namespace Events.Core.Contracts;

/// <summary>
/// Service for deserializing SQS message bodies that may contain direct event data or SNS notifications.
/// </summary>
public interface IMessageDeserializationService
{
    /// <summary>
    /// Deserializes an SQS message body to an IEventBusMessage, handling both direct messages and SNS notifications.
    /// </summary>
    /// <param name="messageBody">The SQS message body as a JSON string.</param>
    /// <returns>The deserialized event object, or null if deserialization fails.</returns>
    IEventBusMessage? DeserializeMessage(string messageBody);
}
