using Events.Core.Contracts;
using Events.Core.Implementations;
using Events.Core.Json;
using log4net;
using Moq;
using NUnit.Framework;
using ClientTrackingSQSLambda.Models;

namespace ClientTrackingSQSLambda.Tests.Events.Core
{
    [TestFixture]
    public class MessageDeserializationServiceTests
    {
        private Mock<ILog> _mockLogger;
        private MessageDeserializationService _deserializationService;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILog>();
            _deserializationService = new MessageDeserializationService(_mockLogger.Object);
            EventBusJsonConverter.RegisterEventType("ClientTracking", typeof(ClientTrackingEvent));
        }

        [Test]
        public void DeserializeMessage_DirectMessage_ReturnsEventBusMessage()
        {
            // Arrange
            var directMessage = @"{
                ""IpAddress"": ""192.168.21.210"",
                ""MacAddress"": ""1EC42DEFCBGG"",
                ""UserAgentRaw"": ""Mozilla/5.0 (iPhone; CPU iPhone OS 18_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148"",
                ""ClientDeviceTypeId"": ""Smartphone"",
                ""PlatformTypeId"": ""Ios"",
                ""BrowserTypeId"": ""Safari"",
                ""MemberName"": ""hs-0275"",
                ""MemberId"": 261657,
                ""MemberNumber"": ""1086-GA-99"",
                ""OrgNumber"": ""FO-640-74"",
                ""ZoneType"": ""Public"",
                ""GpnsEnabled"": false,
                ""ZonePlanName"": ""Basic Access"",
                ""TimeZoneId"": ""America/Vancouver"",
                ""Price"": 0.0,
                ""CurrencyCode"": ""USD"",
                ""AuthMethod"": ""CreateFreeUser"",
                ""Subject"": ""ClientTracking"",
                ""ChangeType"": ""Create""
            }";

            // Act
            var result = _deserializationService.DeserializeMessage(directMessage);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<ClientTrackingEvent>());
            var clientEvent = result as ClientTrackingEvent;
            Assert.That(clientEvent?.Subject, Is.EqualTo("ClientTracking"));
            Assert.That(clientEvent?.OrgNumber, Is.EqualTo("FO-640-74"));
            Assert.That(clientEvent?.MacAddress, Is.EqualTo("1EC42DEFCBGG"));
        }

        [Test]
        public void DeserializeMessage_SNSNotification_ExtractsAndReturnsEventBusMessage()
        {
            // Arrange
            var snsMessage = @"{
                ""Type"": ""Notification"",
                ""MessageId"": ""31df276d-4fb9-5d33-8d42-466a4c147dd8"",
                ""TopicArn"": ""arn:aws:sns:us-west-2:265848155493:eleven-event-bus-receive"",
                ""Message"": ""{\""IpAddress\"":\""192.168.21.211\"",\""MacAddress\"":\""1EC42DEFCBHH\"",\""UserAgentRaw\"":\""Mozilla/5.0 (iPhone; CPU iPhone OS 18_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148\"",\""ClientDeviceTypeId\"":\""Smartphone\"",\""PlatformTypeId\"":\""Ios\"",\""BrowserTypeId\"":\""Safari\"",\""MemberName\"":\""hs-0275\"",\""MemberId\"":261657,\""MemberNumber\"":\""1086-GA-99\"",\""OrgNumber\"":\""FO-640-74\"",\""ZoneType\"":\""Public\"",\""GpnsEnabled\"":false,\""ZonePlanName\"":\""Basic Access\"",\""TimeZoneId\"":\""America/Vancouver\"",\""Price\"":0.0,\""CurrencyCode\"":\""USD\"",\""AuthMethod\"":\""CreateFreeUser\"",\""Subject\"":\""ClientTracking\"",\""ChangeType\"":\""Create\""}"",
                ""Timestamp"": ""2025-08-01T18:03:25.793Z"",
                ""SignatureVersion"": ""1"",
                ""Signature"": ""riX/tDzDfzvPXLPTkTg6rpywEVIkHI4PI02DRSx63U7r/qJ/bLGXwThtktXM0rfdKXtC2G49uGtNRQDarJQYxsHJyGNfRSC6yBYcnYQMgeemZea6LK1o0Q01nsJtdcD/RwOzuNUVLuffYnZIXAuWudZvANLrnqfNlAKLzuyu7zUJfq2Xdxh4KkSJ59BgiKFJTN+mOllS5cxKmCD5PTZCc6G4EDJDikPYyOSVCXKtiTgbXWXJJ4LXu6a70nGfzMfpgRdXPDp0eSGLKmZAwTzGNn8ytFbCfwBmVagqKQlxU9mrt7C5cZfibzTFu6OBxjGm1B4oFhrs84Jrg4y8tTTZTQ=="",
                ""SigningCertURL"": ""https://sns.us-west-2.amazonaws.com/SimpleNotificationService-9c6465fa7f48f5cacd23014631ec1136.pem"",
                ""UnsubscribeURL"": ""https://sns.us-west-2.amazonaws.com/?Action=Unsubscribe&SubscriptionArn=arn:aws:sns:us-west-2:265848155493:eleven-event-bus-receive:e3efcede-b7ee-4f12-94ef-d7c773db2baa""
            }";

            // Act
            var result = _deserializationService.DeserializeMessage(snsMessage);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<ClientTrackingEvent>());
            var clientEvent = result as ClientTrackingEvent;
            Assert.That(clientEvent?.Subject, Is.EqualTo("ClientTracking"));
            Assert.That(clientEvent?.OrgNumber, Is.EqualTo("FO-640-74"));
            Assert.That(clientEvent?.MacAddress, Is.EqualTo("1EC42DEFCBHH"));
        }

        [Test]
        public void DeserializeMessage_InvalidJson_ReturnsNull()
        {
            // Arrange
            var invalidJson = "{ invalid json }";

            // Act
            var result = _deserializationService.DeserializeMessage(invalidJson);

            // Assert
            Assert.That(result, Is.Null);
            _mockLogger.Verify(l => l.Error(It.IsAny<object>(), It.IsAny<Exception>()), Times.Once);
        }

        [Test]
        public void DeserializeMessage_SNSWithoutMessage_ReturnsNull()
        {
            // Arrange
            var snsWithoutMessage = @"{
                ""Type"": ""Notification"",
                ""MessageId"": ""31df276d-4fb9-5d33-8d42-466a4c147dd8""
            }";

            // Act
            var result = _deserializationService.DeserializeMessage(snsWithoutMessage);

            // Assert
            Assert.That(result, Is.Null);
            _mockLogger.Verify(l => l.Error(It.IsAny<object>(), It.IsAny<Exception>()), Times.Once);
        }

        [Test]
        public void DeserializeMessage_SNSWithEmptyMessage_ReturnsNull()
        {
            // Arrange
            var snsWithEmptyMessage = @"{
                ""Type"": ""Notification"",
                ""MessageId"": ""31df276d-4fb9-5d33-8d42-466a4c147dd8"",
                ""Message"": """"
            }";

            // Act
            var result = _deserializationService.DeserializeMessage(snsWithEmptyMessage);

            // Assert
            Assert.That(result, Is.Null);
            _mockLogger.Verify(l => l.Error(It.IsAny<object>(), It.IsAny<Exception>()), Times.Once);
        }
    }
}