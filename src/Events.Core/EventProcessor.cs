using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using log4net;
using Eleven.Logging.Lambda;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using log4net.Util;
using Events.Core.Contracts;
using Events.Core;
using Events.Core.Implementations;
using Amazon.SQS;

namespace Events.Core;


// Helper function for processing event data from SQS, aggregating
// Handles business logic for all event types (e.g., ClientTracking).
public class EventProcessor
{
    
    // DynamoDB and SQS wrappers
    // Table name from environment
    
    private readonly ISqsClientWrapper _sqsClient;
    private readonly ILog logger;
    private readonly string _eventsDlqUrl = Environment.GetEnvironmentVariable("EVENTS_DLQ_URL")
        ?? throw new InvalidOperationException("EVENTS_DLQ_URL not in environment variables");

    private static readonly Dictionary<Type, object?> _typeRegistry = [];


    /// <summary>
    /// Default constructor for Lambda runtime. Instantiates wrappers with default implementations.
    /// </summary>
    public EventProcessor()
    {
        var success = _typeRegistry.TryGetValue(typeof(ISqsClientWrapper), out var sqsService);
        if (!success || sqsService == null || sqsService is not ISqsClientWrapper sqsWrapper)
        {
            sqsWrapper = new SqsClientWrapper(new AmazonSQSClient()); // Default implementation
        }
        _sqsClient = sqsWrapper;

        success = _typeRegistry.TryGetValue(typeof(ILog), out var logService);
        if (!success || logService == null || logService is not ILog log)
        {
            throw new InvalidOperationException("ILog service not registered in type registry. Please register ILog service before using EventProcessor.");
        }
        logger = log;
    }


    /// <summary>
    /// Main handler: processes each SQS record, aggregates, and updates DynamoDB.
    /// </summary>
    /// <param name="sqsEvent">The SQS event containing records to process.</param>
    /// <param name="context">The Lambda context object.</param>
    /// <param name="logger">Logger for structured logging.</param>
    public async Task ProcessEventAsync(SQSEvent sqsEvent, ILambdaContext context)
    {
        var cancellationToken = context.GetCTSToken();

        logger.Debug(new
        {
            Message = "EventProcessor invoked",
            RecordCount = sqsEvent.Records.Count,
            LambdaArn = context.InvokedFunctionArn
        });
        if (sqsEvent.Records.Count == 0)
        {
            logger.Warn(new
            {
                Message = "No records found in SQS event",
                sqsEvent
            });
            return;
        }

        foreach (var sqsRecord in sqsEvent.Records)
        {
            // Use legacy JSON escaping fix before deserialization
            var correctedBody = sqsRecord.Body.CorrectJsonEscaping();
            var eventRecord = correctedBody.DeserializeSafe<IEventBusMessage>(logger);
            if (eventRecord == null)
            {
                logger.Error(new
                {
                    Message = "Failed to deserialize SQS record",
                    sqsRecord
                });
                continue;
            }

            // Get the appropriate strategy for this event type
            var serviceProvider = BuildServiceProvider();
            var strategy = eventRecord.GetEventProcessor(serviceProvider);
            try
            {
                // Process event and get update payload
                var processorResponse = await strategy.ProcessEventDataAsync(eventRecord, cancellationToken);
                if (!processorResponse.Success)
                {
                    // Log the failure and send to DLQ
                    logger.Warn(new
                    {
                        Message = "Failed to process event data",
                        EventRecord = eventRecord,
                        processorResponse
                    });
                    throw new InvalidOperationException("Processor response was null or unsuccessful");
                }
            }
            catch (Exception ex)
            {
                logger.Error(new
                {
                    Message = "Error processing event data",
                    EventRecord = eventRecord,
                    sqsRecord
                }, ex);
                await HandleFailedSQSMessage(eventRecord, sqsRecord, logger, cancellationToken);
                continue; // Skip this record on error
            }
            await HandleSuccessfulSQSMessage(sqsRecord, logger, cancellationToken);
            

        }
    }

    private async Task HandleSuccessfulSQSMessage(SQSEvent.SQSMessage sqsRecord, ILog logger, CancellationToken cancellationToken = default)
    {
                
        // Delete the message from SQS after successful processing
        var deleteResponse = await _sqsClient.DeleteSqsMessageAsync(sqsRecord, cancellationToken);
        if (deleteResponse.HttpStatusCode != System.Net.HttpStatusCode.OK)
        {
            logger.Error(new
            {
                Message = "Failed to delete SQS message after processing",
                deleteResponse,
                sqsRecord
            });
        }
    }

    /// <summary>
    /// Handles failed SQS messages by sending them to the DLQ and deleting the original message.
    /// </summary>
    /// <param name="eventRecord"></param>
    /// <param name="sqsRecord"></param>
    /// <param name="logger"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task HandleFailedSQSMessage(IEventBusMessage eventRecord, SQSEvent.SQSMessage sqsRecord, ILog logger, CancellationToken cancellationToken = default)
    {
        var sendResponse = await _sqsClient.SendMessageAsync(_eventsDlqUrl, JsonConvert.SerializeObject(eventRecord), cancellationToken);
        if (sendResponse.HttpStatusCode != System.Net.HttpStatusCode.OK)
        {
            logger.Error(new
            {
                Message = "PostSQSMessageToDLQAndDelete - Failed to send message to DLQ",
                sendResponse,
                sqsRecord,
                EventRecord = eventRecord
            });
            return; // Don't attempt to delete if DLQ send failed
        }
        var deleteResponse = await _sqsClient.DeleteSqsMessageAsync(sqsRecord, cancellationToken);
        if (deleteResponse.HttpStatusCode != System.Net.HttpStatusCode.OK)
        {
            logger.Error(new
            {
                Message = "PostSQSMessageToDLQAndDelete - Unexpected response from sqs.DeleteMessageAsync",
                deleteResponse,
                sqsRecord
            });
        }
    }
    public static bool RegisterService<TService>(object? service) where TService : class
    {
        if (service == null)
        {
            return false;
        }
        //handle duplicate registrations
        _typeRegistry[typeof(TService)] = service;
        return true;
    }
    private static IServiceProvider BuildServiceProvider()
    {
        return new StaticRegistryServiceProvider();
    }

    public static void ClearServiceRegistry()
    {
        _typeRegistry.Clear();
    }

    private sealed class StaticRegistryServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            _typeRegistry.TryGetValue(serviceType, out var service) ? service : null;
    }


}
