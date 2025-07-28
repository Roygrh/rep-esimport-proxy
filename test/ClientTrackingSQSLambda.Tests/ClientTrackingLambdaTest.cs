using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.EventBridge;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SQS.Model;
using ClientTrackingSQSLambda.Models;
using ClientTrackingSQSLambda.Strategies;
using Events.Core;
using Events.Core.Contracts;
using Events.Core.Implementations;
using log4net;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Text.Json;
using static Amazon.Lambda.SQSEvents.SQSEvent;

namespace ClientTrackingSQSLambda.Tests;

[TestFixture]
public class ClientTrackingLambdaTest
{
    private Mock<IDynamoDbClientWrapper> _mockDynamoDbClient;
    private Mock<ISqsClientWrapper> _mockSqsClient;
    private Mock<ILambdaContext> _mockLambdaContext;
    private Mock<IAmazonEventBridge> _mockEventBridge;
    private Mock<ILog> _mockLogger;
    private Mock<IServiceProvider> _mockServiceProvider;
    private EventProcessor _EventHandler;
    private readonly string _defaultOrgNumber = "AB-123-45";
    private readonly TestTools _testTools = new();

    [SetUp]
    public void Setup()
    {
        // Arrange common environment and mocks
        Environment.SetEnvironmentVariable("DYNAMODB_TABLE_NAME", "client-tracking-data");
        Environment.SetEnvironmentVariable("CLIENT_TRACKING_PARTITION_COUNT", "10");
        Environment.SetEnvironmentVariable("EVENTS_DLQ_URL", "some.dlq.url.com");
        AWSConfigs.AWSRegion = "us-west-2";
        _mockSqsClient = new Mock<ISqsClientWrapper>();
        _mockLambdaContext = new Mock<ILambdaContext>();
        _mockEventBridge = new Mock<IAmazonEventBridge>();
        _mockLogger = new Mock<ILog>();
        _mockDynamoDbClient = new Mock<IDynamoDbClientWrapper>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        EventProcessor.RegisterService<ISqsClientWrapper>(_mockSqsClient.Object);
        EventProcessor.RegisterService<ILog>(_mockLogger.Object);

        _EventHandler = new EventProcessor();
        _mockSqsClient.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

    }

    [TearDown]
    public void TearDown()
    {
        EventProcessor.ClearServiceRegistry(); // Clear registered services after each test
    }

    [Test]
    public async Task ProcessEventAsync_Success()
    {
        // Arrange
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
        double remainingTime = 600; // 5 minutes in seconds
        _mockLambdaContext.SetupGet(c => c.RemainingTime).Returns(TimeSpan.FromSeconds(remainingTime));
        _mockDynamoDbClient.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });
        _mockSqsClient.Setup(c => c.DeleteSqsMessageAsync(It.IsAny<SQSMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        var clientTrackingLambda = new ClientTrackingLambda(_mockDynamoDbClient.Object);
        await clientTrackingLambda.FunctionHandlerAsync(sqsEvent, _mockLambdaContext.Object);

        // Assert
        _mockDynamoDbClient.Verify(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockSqsClient.Verify(c => c.DeleteSqsMessageAsync(It.IsAny<SQSMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ProcessEventAsync_NoRecords()
    {
        // Arrange
        var snsEvent = _testTools.GetRandomClientTrackingEvent();
        snsEvent.OrgNumber = _defaultOrgNumber;
        var sqsEvent = new SQSEvent
        {
            Records =[]
        };

        var clientTrackingLambda = new ClientTrackingLambda(_mockDynamoDbClient.Object);
        await clientTrackingLambda.FunctionHandlerAsync(sqsEvent, _mockLambdaContext.Object);

        // Assert
        _mockDynamoDbClient.Verify(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockSqsClient.Verify(c => c.DeleteSqsMessageAsync(It.IsAny<SQSMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ProcessEventAsync_ErrorDeletingFromSQS()
    {
        // Arrange
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
        double remainingTime = 600; // 5 minutes in seconds
        _mockLambdaContext.SetupGet(c => c.RemainingTime).Returns(TimeSpan.FromSeconds(remainingTime));
        _mockDynamoDbClient.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateItemResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });
        _mockSqsClient.Setup(c => c.DeleteSqsMessageAsync(It.IsAny<SQSMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.InternalServerError });

        var clientTrackingLambda = new ClientTrackingLambda(_mockDynamoDbClient.Object);
        await clientTrackingLambda.FunctionHandlerAsync(sqsEvent, _mockLambdaContext.Object);

        // Assert
        _mockDynamoDbClient.Verify(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockSqsClient.Verify(c => c.DeleteSqsMessageAsync(It.IsAny<SQSMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void GetReportStrategy_ThrowsOnNullDdb()
    {
        // Arrange
        var evt = _testTools.GetRandomClientTrackingEvent();
        evt.OrgNumber = _defaultOrgNumber;

        // Null service provider
        Assert.Throws<ArgumentNullException>(() => evt.GetEventProcessor(null!));

        // Null ddb
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IDynamoDbClientWrapper))).Returns((object?)null);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(ILog))).Returns(_mockLogger.Object);
        Assert.Throws<ArgumentNullException>(() => evt.GetEventProcessor(_mockServiceProvider.Object));
    }

    [Test]
    public void EventProcessor_ThrowsIfLoggerNotRegistered()
    {
        // Clear registry to ensure logger is not registered
        EventProcessor.ClearServiceRegistry();

        // Register only DynamoDbClientWrapper
        var mockDynamoDbClient = new Mock<IDynamoDbClientWrapper>();
        EventProcessor.RegisterService<IDynamoDbClientWrapper>(mockDynamoDbClient.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            var processor = new EventProcessor();
        });
    }

    [Test]
    public async Task ProcessEventAsync_ZeroRecords_LogsWarningAndReturns()
    {
        // Arrange
        var sqsEvent = new SQSEvent { Records = [] };
        var clientTrackingLambda = new ClientTrackingLambda(_mockDynamoDbClient.Object);
        // Act
        await _EventHandler.ProcessEventAsync(sqsEvent, _mockLambdaContext.Object);

        // Assert
        _mockLogger.Verify(l => l.Warn(It.IsAny<object>()), Times.Once);
    }

    [Test]
    public async Task ProcessEventDataAsync_InvalidEventType_LogsErrorAndReturnsSuccess()
    {
        var strategy = new ClientTrackingStrategy(_mockDynamoDbClient.Object, _mockLogger.Object);

        // Pass an event that is NOT a ClientTrackingEvent
        var invalidEvent = new Mock<IEventBusMessage>().Object;

        var result = await strategy.ProcessEventDataAsync(invalidEvent);

        Assert.That(result.Success);
        StringAssert.Contains("Invalid event", result.Details);
        _mockLogger.Verify(l => l.Error(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task ProcessEventDataAsync_MissingOrgNumber_LogsErrorAndReturnsSuccess()
    { 
        var strategy = new ClientTrackingStrategy(_mockDynamoDbClient.Object, _mockLogger.Object);

        var evt = new ClientTrackingEvent
        {
            OrgNumber = null, // Missing OrgNumber
            TimeZoneId = "Europe/Oslo",
            MacAddress = "00:11:22:33:44:55"
        };

        var result = await strategy.ProcessEventDataAsync(evt);

        Assert.That(result.Success);
        StringAssert.Contains("Invalid event", result.Details);
        _mockLogger.Verify(l => l.Error(It.IsAny<object>()), Times.Once);
    }

    [Test]
    public async Task ProcessEventDataAsync_MissingTimeZoneId_LogsErrorAndReturnsSuccess()
    {
        var strategy = new ClientTrackingStrategy(_mockDynamoDbClient.Object, _mockLogger.Object);

        var evt = new ClientTrackingEvent
        {
            OrgNumber = "AB-123-45",
            TimeZoneId = null, // Missing TimeZoneId
            MacAddress = "00:11:22:33:44:55"
        };

        var result = await strategy.ProcessEventDataAsync(evt);

        Assert.That(result.Success);
        StringAssert.Contains("Invalid event", result.Details);
        _mockLogger.Verify(l => l.Error(It.IsAny<object>()), Times.Once);
    }

    [Test]
    public async Task ProcessEventDataAsync_MissingMacAddress_LogsErrorAndReturnsSuccess()
    {
        var strategy = new ClientTrackingStrategy(_mockDynamoDbClient.Object, _mockLogger.Object);

        var evt = new ClientTrackingEvent
        {
            OrgNumber = "AB-123-45",
            TimeZoneId = "Europe/Oslo",
            MacAddress = null // Missing MacAddress
        };

        var result = await strategy.ProcessEventDataAsync(evt);

        Assert.That(result.Success);
        StringAssert.Contains("Invalid event", result.Details);
        _mockLogger.Verify(l => l.Error(It.IsAny<object>()), Times.Once);
    }

    [Test]
    public async Task ProcessEventDataAsync_DynamoDbThrows_LogsErrorAndReturnsFailure()
    {
        // Arrange
        var strategy = new ClientTrackingStrategy(_mockDynamoDbClient.Object, _mockLogger.Object);

        var evt = new ClientTrackingEvent
        {
            OrgNumber = "AB-123-45",
            TimeZoneId = "Europe/Oslo",
            MacAddress = "00:11:22:33:44:55",
            Subject = "ClientTracking"
        };

        var exception = new Exception("DynamoDB failure");
        _mockDynamoDbClient
            .Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var result = await strategy.ProcessEventDataAsync(evt);

        // Assert
        Assert.That(!result.Success);
        StringAssert.Contains("Error saving to DynamoDB", result.Details);
        _mockLogger.Verify(
            l => l.Error("Failed to save client event to DynamoDB", exception),
            Times.Once);
    }

    [Test]
    public void ClientTrackingLambda_ParameterlessConstructor_CreatesInstance()
    {
        // Act
        var lambda = new ClientTrackingLambda();

        // Assert
        Assert.That(lambda, Is.Not.Null);
        Assert.That(lambda, Is.InstanceOf<ClientTrackingLambda>());
    }

    [Test]
    public void Dispose_CallsDisposeOnDynamoDbClientWrapper()
    {
        // Arrange
        var strategy = new ClientTrackingStrategy(_mockDynamoDbClient.Object, _mockLogger.Object);

        // Act
        strategy.Dispose();

        // Assert
        _mockDynamoDbClient.Verify(d => d.Dispose(), Times.Once);
    }

    [Test]
    public void Dispose_CallsDisposeOnDynamoDbClientWrapper_nullDynamoDb()
    {
        // Arrange
        var strategy = new ClientTrackingStrategy(null, _mockLogger.Object);

        // Act
        strategy.Dispose();

        // Assert
        _mockDynamoDbClient.Verify(d => d.Dispose(), Times.Never);
    }

    [Test]
    public void Dispose_CanBeCalledMultipleTimes_SafeAndIdempotent()
    {
        // Arrange
        var strategy = new ClientTrackingStrategy(_mockDynamoDbClient.Object, _mockLogger.Object);

        // Act
        strategy.Dispose();
        strategy.Dispose(); // Should not throw or call Dispose again

        // Assert
        _mockDynamoDbClient.Verify(d => d.Dispose(), Times.Once);
    }

    [Test]
    public void ExtractEventMessageFromSQS_DoesNotThrowIfNoMessageProperty()
    {
        // Test that an empty body does not throw.
        var result = string.Empty.DeserializeSafe<IEventBusMessage>();
        Assert.That(result, Is.Null, "Expected null result for empty body");
    }

    [Test]
    public void ExtractEventMessageFromSQS_DoesNotThrowOnInvalidJson()
    {
        "not-json".DeserializeSafe<IEventBusMessage>();
        Assert.Pass("Deserialization of invalid JSON did not throw as expected.");
    }

    [Test]
    public void ExtractEventMessageFromSQS_DeserializesNonPolymorphicType()
    {
        var payload = new { Foo = "Bar" };
        var result = JsonSerializer.Serialize(payload).DeserializeSafe<dynamic>();
        Assert.That(result?.Foo?.ToString(), Is.EqualTo("Bar"));
    }
}

