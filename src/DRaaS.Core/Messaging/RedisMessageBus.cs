using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DRaaS.Core.Messaging;

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

                        var message = JsonSerializer.Deserialize<T>((string)json!);
                        if (message == null)
                        {
                            _logger.LogWarning("Failed to deserialize message on channel '{Channel}'", channel);
                            return;
                        }

                        _logger.LogDebug(
                            "Received {MessageType} on channel '{Channel}' (MessageId: {MessageId})",
                            typeof(T).Name,
                            channel,
                            (message as Message)?.MessageId);

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
            // Publish request
            await PublishAsync(channel, request, cancellationToken);

            // Wait for response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                return await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Request to channel '{channel}' timed out after {timeout}");
            }
        }
        finally
        {
            // Unsubscribe from response channel
            var subscriber = _redis.GetSubscriber();
            await subscriber.UnsubscribeAsync(RedisChannel.Literal(responseChannel));
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
