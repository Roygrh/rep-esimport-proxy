using log4net;

namespace Events.Core.Contracts;

/// <summary>
/// Interface for all report data events processed by the event handler.
/// </summary>
public interface IEventBusMessage
{

    /// <summary>
    /// Gets or sets the subject of the report data.
    /// </summary>
    public string Subject { get; set; }

    /// Returns the event processor (strategy) for this event type.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies.</param>
    /// <returns>An IEventBusMessageProcessor for processing this event.</returns>
    public IEventBusMessageProcessor GetEventProcessor(IServiceProvider serviceProvider);
}
