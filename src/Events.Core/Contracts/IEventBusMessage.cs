using log4net;

namespace Events.Core.Contracts;

/// <summary>
/// Interface for all report data events processed by the event handler.
/// </summary>
public interface IEventBusMessage
{
    /// <summary>
    /// Gets or sets the origin of the report data.
    /// </summary>
    public string Origin { get; set; }

    /// <summary>
    /// Gets or sets the subject of the report data.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the scope of the report data.
    /// </summary>
    public string Scope { get; set; }

    /// <summary>
    /// Gets or sets the change type of the report data.
    /// </summary>
    public string ChangeType { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the report data event.
    /// </summary>
    public DateTime DateTime { get; set; }

    /// <summary>
    /// Gets or sets the schema name of the report data.
    /// </summary>
    public string SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the schema version of the report data.
    /// </summary>
    public string SchemaVersion { get; set; }

    /// <summary>
    /// Returns the event processor (strategy) for this event type.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies.</param>
    /// <returns>An IEventBusMessageProcessor for processing this event.</returns>
    public IEventBusMessageProcessor GetEventProcessor(IServiceProvider serviceProvider);
}
