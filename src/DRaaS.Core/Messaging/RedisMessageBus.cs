using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DRaaS.Core.Messaging;

/// <summary>
/// Wrapper for request/response pattern messages.
/// </summary>
internal class RequestWrapper<T>
{
    public T? Request { get; set; }
    public string? ReplyChannel { get; set; }
}

/// <summary>
/// Redis-based message bus implementation using Pub/Sub.
/// Supports both single-host and distributed Redis deployments.
/// </summary>
public class RedisMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisMessageBus> _logger;
    private readonly Dictionary<string, ISubscriber> _subscribers = new();
    private readonly SemaphoreSlim _subscribeLock = new(1, 1);

    public RedisMessageBus(IConnectionMultiplexer redis, ILogger<RedisMessageBus> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<T>(string channel, T message, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(message);

        var subscriber = _redis.GetSubscriber();
        var json = JsonSerializer.Serialize(message);

        var count = await subscriber.PublishAsync(RedisChannel.Literal(channel), json);

        _logger.LogDebug(
            "Published {MessageType} to channel '{Channel}' (MessageId: {MessageId}, Subscribers: {Count})",
            typeof(T).Name,
            channel,
            (message as Message)?.MessageId,
            count);
    }

    public async Task SubscribeAsync<T>(string channel, Func<T, Task> handler, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(handler);

        await _subscribeLock.WaitAsync(cancellationToken);
        try
        {
            var subscriber = _redis.GetSubscriber();

            await subscriber.SubscribeAsync(
                RedisChannel.Literal(channel),
                async (ch, json) =>
                {
                    try
                    {
                        if (json.IsNullOrEmpty)
                        {
                            _logger.LogWarning("Received empty message on channel '{Channel}'", channel);
                            return;
                        }

                        string? replyChannel = null;
                        T? message = null;

                        // Try to deserialize as wrapped request first
                        try
                        {
                            var jsonNode = JsonNode.Parse((string)json!);
                            if (jsonNode?["Request"] != null && jsonNode["ReplyChannel"] != null)
                            {
                                // This is a wrapped request/response message
                                replyChannel = jsonNode["ReplyChannel"]?.GetValue<string>();
                                message = jsonNode["Request"]?.Deserialize<T>();

                                _logger.LogDebug(
                                    "Received request {MessageType} on channel '{Channel}' with reply channel '{ReplyChannel}'",
                                    typeof(T).Name,
                                    channel,
                                    replyChannel);
                            }
                        }
                        catch
                        {
                            // Not a wrapped message, try normal deserialization
                        }

                        // If not wrapped, deserialize normally
                        if (message == null)
                        {
                            message = JsonSerializer.Deserialize<T>((string)json!);

                            _logger.LogDebug(
                                "Received {MessageType} on channel '{Channel}' (MessageId: {MessageId})",
                                typeof(T).Name,
                                channel,
                                (message as Message)?.MessageId);
                        }

                        if (message == null)
                        {
                            _logger.LogWarning("Failed to deserialize message on channel '{Channel}'", channel);
                            return;
                        }

                        // If message has ReplyChannel property, set it
                        if (message is Message msg && !string.IsNullOrEmpty(replyChannel))
                        {
                            message = (T)(object)(msg with { ReplyChannel = replyChannel });
                        }

                        await handler(message);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "JSON deserialization error on channel '{Channel}'", channel);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message on channel '{Channel}'", channel);
                    }
                });

            _subscribers[channel] = subscriber;

            _logger.LogInformation("Subscribed to channel '{Channel}' for {MessageType}", channel, typeof(T).Name);
        }
        finally
        {
            _subscribeLock.Release();
        }
    }

    public async Task<TResponse> RequestAsync<TRequest, TResponse>(
        string channel,
        TRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(request);

        var responseChannel = $"{channel}.response.{Guid.NewGuid()}";
        var tcs = new TaskCompletionSource<TResponse>();

        // Subscribe to response channel (one-time)
        await SubscribeAsync<TResponse>(responseChannel, async response =>
        {
            tcs.TrySetResult(response);
            await Task.CompletedTask;
        }, cancellationToken);

        try
        {
            // Wrap the request with reply channel information
            var requestWithReply = new
            {
                Request = request,
                ReplyChannel = responseChannel
            };

            // Serialize and publish the wrapped request
            var subscriber = _redis.GetSubscriber();
            var json = JsonSerializer.Serialize(requestWithReply);
            await subscriber.PublishAsync(RedisChannel.Literal(channel), json);

            _logger.LogDebug(
                "Sent request {MessageType} to channel '{Channel}', waiting for response on '{ResponseChannel}'",
                typeof(TRequest).Name,
                channel,
                responseChannel);

            // Wait for response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                return await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Request {MessageType} to channel '{Channel}' timed out after {Timeout}",
                    typeof(TRequest).Name,
                    channel,
                    timeout);
                throw new TimeoutException($"Request to channel '{channel}' timed out after {timeout}");
            }
        }
        finally
        {
            // Unsubscribe from response channel
            var subscriber = _redis.GetSubscriber();
            await subscriber.UnsubscribeAsync(RedisChannel.Literal(responseChannel));
            _subscribers.Remove(responseChannel);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _subscribeLock.WaitAsync();
        try
        {
            // Unsubscribe from all channels
            var subscriber = _redis.GetSubscriber();
            foreach (var channel in _subscribers.Keys)
            {
                await subscriber.UnsubscribeAsync(RedisChannel.Literal(channel));
                _logger.LogInformation("Unsubscribed from channel '{Channel}'", channel);
            }

            _subscribers.Clear();
        }
        finally
        {
            _subscribeLock.Release();
            _subscribeLock.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
