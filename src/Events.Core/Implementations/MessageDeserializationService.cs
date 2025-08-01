using Amazon.Lambda.SNSEvents;
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
            _logger.Debug(new
            {
                Message = "Processing message body",
                OriginalBody = messageBody
            });

            // Try to parse as SNS notification first
            var snsMessage = messageBody.DeserializeFromJsonSafe<SNSEvent.SNSMessage>(propertyNameCaseSensitive: false);
            string messageToProcess;

            if (snsMessage?.Type == "Notification" && !string.IsNullOrEmpty(snsMessage.Message))
            {
                // This is an SNS notification - extract the inner message
                messageToProcess = snsMessage.Message;
                _logger.Debug(new
                {
                    Message = "Detected SNS notification, extracting inner message",
                    InnerMessage = messageToProcess
                });
            }
            else
            {
                // This is a direct message or SNS parsing failed - try as direct message
                messageToProcess = messageBody;
                _logger.Debug("Processing as direct message");
            }

            // Try to deserialize the message content, with fallback to escape correction
            IEventBusMessage? result = null;
            try
            {
                result = JsonSerializer.Deserialize<IEventBusMessage>(messageToProcess, _jsonOptions);
            }
            catch (JsonException)
            {
                // If that fails, try with escape correction
                _logger.Debug("Initial deserialization failed, attempting with escape correction");
                var correctedMessage = messageToProcess.CorrectJsonEscaping();
                result = JsonSerializer.Deserialize<IEventBusMessage>(correctedMessage, _jsonOptions);
            }

            return result;
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
