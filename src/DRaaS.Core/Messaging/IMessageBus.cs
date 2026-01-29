namespace DRaaS.Core.Messaging;

/// <summary>
/// Message bus abstraction for publishing commands/events and subscribing to channels.
/// Implementations can be in-memory, Redis, NATS, RabbitMQ, etc.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publish a message to a channel.
    /// </summary>
    Task PublishAsync<T>(string channel, T message, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Subscribe to a channel and process messages with a handler.
    /// </summary>
    Task SubscribeAsync<T>(string channel, Func<T, Task> handler, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Request/Response pattern for synchronous queries.
    /// </summary>
    Task<TResponse> RequestAsync<TRequest, TResponse>(
        string channel,
        TRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;
}
