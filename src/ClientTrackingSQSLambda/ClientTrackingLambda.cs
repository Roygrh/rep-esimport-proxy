using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using ClientTrackingSQSLambda.Models;
using Eleven.Logging.Lambda;
using Events.Core;
using Events.Core.Contracts;
using Events.Core.Implementations;
using Events.Core.Json;
using log4net;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ClientTrackingSQSLambda;

public class ClientTrackingLambda : LoggedFunction<ClientTrackingLambda>
{
    static ClientTrackingLambda()
    {
        EventBusJsonConverter.RegisterEventType("ClientTracking", typeof(ClientTrackingEvent));
    }
    public ClientTrackingLambda() : this(new DynamoDbClientWrapper()) { }

    public ClientTrackingLambda(IDynamoDbClientWrapper ddbClient)
        : base()
    {
        // Client Tracking event handler require dynamoDB client.
        EventProcessor.RegisterService<IDynamoDbClientWrapper>(ddbClient);
    }

    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task FunctionHandlerAsync(SQSEvent sqsEvent, ILambdaContext context)
    {
        await WithLogging(sqsEvent, context, async logger =>
        {
            EventProcessor.RegisterService<ILog>(logger);
            EventProcessor.RegisterService<IMessageDeserializationService>(new MessageDeserializationService(logger));
            logger.Debug(new
            {
                Message = "SaveClientTrackingLambda invoked",
                RecordCount = sqsEvent.Records.Count,
                LambdaArn = context.InvokedFunctionArn
            });

            if (sqsEvent.Records.Count == 0)
            {
                logger.Warn("No records to process in SQS event.");
                return;
            }

            await new EventProcessor().ProcessEventAsync(sqsEvent, context);
        });
    }
}

