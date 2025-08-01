using Events.Core.Contracts;
using Events.Core.Json;
using log4net;
using System.Text.Json;

namespace Events.Core.Implementations;

/// <summary>
/// Service for deserializing SQS message bodies that may contain direct event data or SNS notifications.
/// </summary>
public class MessageDeserializationService : IMessageDeserializationService
{
    private readonly ILog _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public MessageDeserializationService(ILog logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new EventBusJsonConverter() }
        };
    }

    /// <summary>
    /// Deserializes an SQS message body to an IEventBusMessage, handling both direct messages and SNS notifications.
    /// </summary>
    /// <param name="messageBody">The SQS message body as a JSON string.</param>
    /// <returns>The deserialized event object, or null if deserialization fails.</returns>
    public IEventBusMessage? DeserializeMessage(string messageBody)
    {
        try
        {
            // First, try to parse the message body as-is to check if it's valid JSON
            JsonDocument? jsonDoc = null;
            string bodyToParse = messageBody;

            try
            {
                jsonDoc = JsonDocument.Parse(messageBody);
            }
            catch (JsonException)
            {
                // If parsing fails, try with JSON escaping correction
                _logger.Debug("Initial parse failed, attempting with escape correction");
                bodyToParse = messageBody.CorrectJsonEscaping();
                jsonDoc = JsonDocument.Parse(bodyToParse);
            }

            using (jsonDoc)
            {
                var root = jsonDoc.RootElement;

                _logger.Debug(new
                {
                    Message = "Successfully parsed JSON",
                    ParsedBody = bodyToParse
                });

                // Check if this is an SNS notification by looking for Type and Message properties
                if (root.TryGetProperty("Type", out var typeProperty) &&
                    typeProperty.GetString() == "Notification" &&
                    root.TryGetProperty("Message", out var messageProperty))
                {
                    // This is an SNS notification - extract the inner message
                    var innerMessage = messageProperty.GetString();
                    if (string.IsNullOrEmpty(innerMessage))
                    {
                        _logger.Error("SNS Message property is null or empty");
                        return null;
                    }

                    // Deserialize the inner message as the actual event
                    return JsonSerializer.Deserialize<IEventBusMessage>(innerMessage, _jsonOptions);
                }
                else
                {
                    // This is a direct message - deserialize as is
                    _logger.Debug("Detected direct message, deserializing as-is");
                    return JsonSerializer.Deserialize<IEventBusMessage>(bodyToParse, _jsonOptions);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.Error(new
            {
                Message = "Failed to deserialize message",
                MessageContent = messageBody
            }, ex);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(new
            {
                Message = "Unexpected error during message deserialization",
                MessageContent = messageBody
            }, ex);
            return null;
        }
    }
}
