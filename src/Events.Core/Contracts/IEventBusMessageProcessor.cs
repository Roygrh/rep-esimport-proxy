using log4net;

namespace Events.Core.Contracts;

/// <summary>
/// Interface for event processing strategies. Each event type implements its own strategy.
/// </summary>
public interface IEventBusMessageProcessor
{
    /// <summary>
    /// Processes the event and returns a ClientTrackingAggregatePayload for DynamoDB update.
    /// </summary>
    /// <param name="message">The event to process.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>ClientTrackingAggregatePayload with properties to update.</returns>
    Task<IEventBusProcessorResponse> ProcessEventDataAsync(IEventBusMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Marker interface for event bus processor responses.
/// </summary>
public interface IEventBusProcessorResponse
{

    public bool Success { get; set; }
    public string Details { get; set; }
    public object? Context { get; set; }
}