using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using ClientTrackingSQSLambda.Models;
using ClientTrackingSQSLambda.Tests;
using global::Events.Core.Contracts;
using log4net;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static Amazon.Lambda.SQSEvents.SQSEvent;

namespace Events.Core.Tests
{
    [TestFixture]
    public class EventProcessorTests
    {
        private Mock<ISqsClientWrapper> _mockSqsClient;
        private Mock<ILog> _mockLogger;
        private Mock<IEventBusMessageProcessor> _mockStrategy;
        private Mock<ILambdaContext> _mockLambdaContext;
        private Mock<IDynamoDbClientWrapper> _mockDynamoDbClient = new Mock<IDynamoDbClientWrapper>();
        private readonly TestTools _testTools = new();
        private readonly string _defaultOrgNumber = "AB-123-45";

        [SetUp]
        public void Setup()
        {
            // Set all required environment variables
            Environment.SetEnvironmentVariable("EVENTS_DLQ_URL", "https://sqs.us-west-2.amazonaws.com/123456789012/DLQ");
            Environment.SetEnvironmentVariable("AGGREGATES_TABLE", "AggregatesTable");
            Environment.SetEnvironmentVariable("EXPORT_GSI_PARTITION_COUNT", "10");

            _mockSqsClient = new Mock<ISqsClientWrapper>();
            _mockLogger = new Mock<ILog>();
            _mockStrategy = new Mock<IEventBusMessageProcessor>();
            _mockLambdaContext = new Mock<ILambdaContext>();
            _mockLambdaContext.SetupGet(c => c.RemainingTime).Returns(TimeSpan.FromMinutes(5));

            EventProcessor.RegisterService<ISqsClientWrapper>(_mockSqsClient.Object);
            EventProcessor.RegisterService<ILog>(_mockLogger.Object);
            EventProcessor.RegisterService<IDynamoDbClientWrapper>(_mockDynamoDbClient.Object);
            EventProcessor.RegisterService<IEventBusMessageProcessor>(_mockStrategy.Object);
        }

        [TearDown]
        public void TearDown()
        {
            EventProcessor.ClearServiceRegistry();
        }

        [Test]
        public async Task ProcessEventAsync_NoRecords_LogsWarning()
        {
            var sqsEvent = new SQSEvent { Records = new List<SQSEvent.SQSMessage>() };
            var processor = new EventProcessor();

            await processor.ProcessEventAsync(sqsEvent, _mockLambdaContext.Object);

            _mockLogger.Verify(l => l.Warn(It.IsAny<object>()), Times.Once);
        }      

        [Test]
        public async Task ProcessEventAsync_DeserializationFails_LogsError()
        {
            var sqsMessage = new SQSEvent.SQSMessage { Body = "invalid json" };
            var sqsEvent = new SQSEvent { Records = new List<SQSEvent.SQSMessage> { sqsMessage } };

            var processor = new EventProcessor();
            await processor.ProcessEventAsync(sqsEvent, _mockLambdaContext.Object);

            _mockLogger.Verify(l => l.Error(It.IsAny<object>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task ProcessEventAsync_Success()
        {
            var sqsMessage = new SQSMessage { Body = "{}" };
            var snsEvent = _testTools.GetRandomClientTrackingEvent();
            snsEvent.OrgNumber = _defaultOrgNumber;
            var sqsEvent = new SQSEvent
            {
                Records =
            [
                new SQSMessage
                {
                    EventSource = "aws:sqs",
                    AwsRegion = "us-west-2",
                    Body = JsonSerializer.Serialize(snsEvent),
                    EventSourceArn = "arn:aws:sqs:us-west-2:123456789012:YourQueueName",
                    ReceiptHandle = "someReceiptHandle"
                }
            ]
            };

            Json.EventBusJsonConverter.RegisterEventType("ClientTracking", typeof(ClientTrackingEvent));

            _mockStrategy.Setup(s => s.ProcessEventDataAsync(It.IsAny<IEventBusMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ClientTrackingProcessorResponse { Success = true });

            _mockSqsClient.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Amazon.SQS.Model.SendMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

            _mockSqsClient.Setup(s => s.DeleteSqsMessageAsync(It.IsAny<SQSEvent.SQSMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Amazon.SQS.Model.DeleteMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

            var processor = new EventProcessor();
            await processor.ProcessEventAsync(sqsEvent, _mockLambdaContext.Object);

            _mockDynamoDbClient.Verify(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            _mockSqsClient.Verify(s => s.DeleteSqsMessageAsync(It.IsAny<SQSMessage>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task ProcessEventAsync_ProcessorResponseNotSuccess_LogsWarningAndSendMessages()
        {
            // Arrange
            var sqsMessage = new SQSMessage { Body = "{}" };
            var snsEvent = _testTools.GetRandomClientTrackingEvent();
            snsEvent.OrgNumber = _defaultOrgNumber;
            var sqsEvent = new SQSEvent
            {
                Records =
            [
                new SQSMessage
                {
                    EventSource = "aws:sqs",
                    AwsRegion = "us-west-2",
                    Body = JsonSerializer.Serialize(snsEvent),
                    EventSourceArn = "arn:aws:sqs:us-west-2:123456789012:YourQueueName",
                    ReceiptHandle = "someReceiptHandle"
                }
            ]
            };

            Json.EventBusJsonConverter.RegisterEventType("ClientTracking", typeof(ClientTrackingEvent));

            // Mock strategy to return unsuccessful response
            _mockStrategy.Setup(s => s.ProcessEventDataAsync(It.IsAny<IEventBusMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ClientTrackingProcessorResponse { Success = false });

            _mockSqsClient.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Amazon.SQS.Model.SendMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

            _mockSqsClient.Setup(s => s.DeleteSqsMessageAsync(It.IsAny<SQSEvent.SQSMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Amazon.SQS.Model.DeleteMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

            _mockDynamoDbClient.Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("DynamoDB error"));

            // Patch GetEventProcessor to return our mock strategy
            var mockEvent = new Mock<IEventBusMessage>();
            mockEvent.Setup(e => e.GetEventProcessor(It.IsAny<IServiceProvider>())).Returns(_mockStrategy.Object);
            sqsMessage.Body = JsonSerializer.Serialize(mockEvent.Object);

            var processor = new EventProcessor();

            // Act & Assert
            await processor.ProcessEventAsync(sqsEvent, _mockLambdaContext.Object);
            _mockLogger.Verify(l => l.Warn(It.IsAny<object>()), Times.AtLeastOnce);
            _mockSqsClient.Verify(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockSqsClient.Verify(s => s.DeleteSqsMessageAsync(It.IsAny<SQSEvent.SQSMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ProcessEventAsync_ProcessorResponseNotSuccess_LogsWarning_FailSendSQSMessage ()
        {
            // Arrange
            var sqsMessage = new SQSMessage { Body = "{}" };
            var snsEvent = _testTools.GetRandomClientTrackingEvent();
            snsEvent.OrgNumber = _defaultOrgNumber;
            var sqsEvent = new SQSEvent
            {
                Records =
            [
                new SQSMessage
                {
                    EventSource = "aws:sqs",
                    AwsRegion = "us-west-2",
                    Body = JsonSerializer.Serialize(snsEvent),
                    EventSourceArn = "arn:aws:sqs:us-west-2:123456789012:YourQueueName",
                    ReceiptHandle = "someReceiptHandle"
                }
            ]
            };

            Json.EventBusJsonConverter.RegisterEventType("ClientTracking", typeof(ClientTrackingEvent));

            // Mock strategy to return unsuccessful response
            _mockStrategy.Setup(s => s.ProcessEventDataAsync(It.IsAny<IEventBusMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ClientTrackingProcessorResponse { Success = false });

            _mockSqsClient.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Amazon.SQS.Model.SendMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.InternalServerError });

            _mockSqsClient.Setup(s => s.DeleteSqsMessageAsync(It.IsAny<SQSEvent.SQSMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Amazon.SQS.Model.DeleteMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

            _mockDynamoDbClient.Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("DynamoDB error"));

            // Patch GetEventProcessor to return our mock strategy
            var mockEvent = new Mock<IEventBusMessage>();
            mockEvent.Setup(e => e.GetEventProcessor(It.IsAny<IServiceProvider>())).Returns(_mockStrategy.Object);
            sqsMessage.Body = JsonSerializer.Serialize(mockEvent.Object);

            var processor = new EventProcessor();

            // Act & Assert
            await processor.ProcessEventAsync(sqsEvent, _mockLambdaContext.Object);
            _mockLogger.Verify(l => l.Warn(It.IsAny<object>()), Times.AtLeastOnce);
            _mockLogger.Verify(l => l.Error(It.IsAny<object>()), Times.AtLeastOnce);
            _mockSqsClient.Verify(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockSqsClient.Verify(s => s.DeleteSqsMessageAsync(It.IsAny<SQSEvent.SQSMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        }


        [Test]
        public async Task ProcessEventAsync_ProcessorResponseNotSuccess_LogsWarning_FailDeleteSQSMessage()
        {
            // Arrange
            var sqsMessage = new SQSMessage { Body = "{}" };
            var snsEvent = _testTools.GetRandomClientTrackingEvent();
            snsEvent.OrgNumber = _defaultOrgNumber;
            var sqsEvent = new SQSEvent
            {
                Records =
            [
                new SQSMessage
                {
                    EventSource = "aws:sqs",
                    AwsRegion = "us-west-2",
                    Body = JsonSerializer.Serialize(snsEvent),
                    EventSourceArn = "arn:aws:sqs:us-west-2:123456789012:YourQueueName",
                    ReceiptHandle = "someReceiptHandle"
                }
            ]
            };

            Json.EventBusJsonConverter.RegisterEventType("ClientTracking", typeof(ClientTrackingEvent));

            // Mock strategy to return unsuccessful response
            _mockStrategy.Setup(s => s.ProcessEventDataAsync(It.IsAny<IEventBusMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ClientTrackingProcessorResponse { Success = false });

            _mockSqsClient.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Amazon.SQS.Model.SendMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

            _mockSqsClient.Setup(s => s.DeleteSqsMessageAsync(It.IsAny<SQSEvent.SQSMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Amazon.SQS.Model.DeleteMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.InternalServerError });

            _mockDynamoDbClient.Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("DynamoDB error"));

            // Patch GetEventProcessor to return our mock strategy
            var mockEvent = new Mock<IEventBusMessage>();
            mockEvent.Setup(e => e.GetEventProcessor(It.IsAny<IServiceProvider>())).Returns(_mockStrategy.Object);
            sqsMessage.Body = JsonSerializer.Serialize(mockEvent.Object);

            var processor = new EventProcessor();

            // Act & Assert
            await processor.ProcessEventAsync(sqsEvent, _mockLambdaContext.Object);
            _mockLogger.Verify(l => l.Warn(It.IsAny<object>()), Times.AtLeastOnce);
            _mockLogger.Verify(l => l.Error(It.IsAny<object>()), Times.AtLeastOnce);
            _mockSqsClient.Verify(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockSqsClient.Verify(s => s.DeleteSqsMessageAsync(It.IsAny<SQSEvent.SQSMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void RegisterService_ReturnsFalse_WhenServiceIsNull()
        {
            // Act
            var result = EventProcessor.RegisterService<object>(null);

            // Assert
            Assert.That(result.Equals(false));
        }
    }
}
