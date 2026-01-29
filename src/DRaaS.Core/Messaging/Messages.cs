namespace DRaaS.Core.Messaging;

/// <summary>
/// Base class for all messages in the system.
/// </summary>
public abstract record Message
{
    /// <summary>
    /// Unique identifier for this message.
    /// </summary>
    public Guid MessageId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// When this message was created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Correlation ID for tracking related messages.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Reply channel for request/response patterns.
    /// Used by RequestAsync to specify where the response should be sent.
    /// </summary>
    public string? ReplyChannel { get; init; }
}

/// <summary>
/// Base class for commands (requests to perform an action).
/// </summary>
public abstract record Command : Message;

/// <summary>
/// Base class for events (notifications that something happened).
/// </summary>
public abstract record Event : Message;

/// <summary>
/// Base class for queries (requests for information).
/// </summary>
public abstract record Query : Message;

/// <summary>
/// Base class for query responses.
/// </summary>
public abstract record QueryResponse : Message;
