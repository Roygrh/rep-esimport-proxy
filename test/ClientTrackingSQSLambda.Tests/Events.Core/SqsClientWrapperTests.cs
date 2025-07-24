using Amazon.Lambda.SQSEvents;
using Amazon.SQS;
using Amazon.SQS.Model;
using Events.Core.Implementations;
using Moq;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace ClientTrackingSQSLambda.Tests.Events.Core
{
    [TestFixture]
    public class SqsClientWrapperTests
    {
        private Mock<IAmazonSQS> _mockSqsClient;
        private SqsClientWrapper _wrapper;

        [SetUp]
        public void Setup()
        {
            _mockSqsClient = new Mock<IAmazonSQS>();
            _wrapper = new SqsClientWrapper(_mockSqsClient.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _wrapper.Dispose();
        }

        [Test]
        public async Task DeleteSqsMessageAsync_ValidRecord_DeletesMessage()
        {
            var sqsRecord = new SQSEvent.SQSMessage
            {
                EventSourceArn = "arn:aws:sqs:us-west-2:123456789012:TestQueue",
                AwsRegion = "us-west-2",
                ReceiptHandle = "test-receipt"
            };

            _mockSqsClient
                .Setup(s => s.DeleteMessageAsync(
                    "https://sqs.us-west-2.amazonaws.com/123456789012/TestQueue",
                    "test-receipt",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

            var response = await _wrapper.DeleteSqsMessageAsync(sqsRecord);

            Assert.That(System.Net.HttpStatusCode.OK, Is.EqualTo(response.HttpStatusCode));
            _mockSqsClient.VerifyAll();
        }

        [Test]
        public void DeleteSqsMessageAsync_InvalidArn_Throws()
        {
            var sqsRecord = new SQSEvent.SQSMessage
            {
                EventSourceArn = "invalid-arn",
                AwsRegion = "us-west-2",
                ReceiptHandle = "test-receipt"
            };

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _wrapper.DeleteSqsMessageAsync(sqsRecord));
        }

        [Test]
        public async Task SendMessageAsync_ValidRequest_SendsMessage()
        {
            var queueUrl = "https://sqs.us-west-2.amazonaws.com/123456789012/TestQueue";
            var messageBody = "test-message";

            _mockSqsClient
                .Setup(s => s.SendMessageAsync(
                    It.Is<SendMessageRequest>(r => r.QueueUrl == queueUrl && r.MessageBody == messageBody),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SendMessageResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

            var response = await _wrapper.SendMessageAsync(queueUrl, messageBody);

            Assert.That(response.HttpStatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
            _mockSqsClient.VerifyAll();
        }

        [Test]
        public void Methods_AfterDispose_ThrowObjectDisposedException()
        {
            _wrapper.Dispose();

            var sqsRecord = new SQSEvent.SQSMessage
            {
                EventSourceArn = "arn:aws:sqs:us-west-2:123456789012:TestQueue",
                AwsRegion = "us-west-2",
                ReceiptHandle = "test-receipt"
            };

            Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await _wrapper.DeleteSqsMessageAsync(sqsRecord));

            Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await _wrapper.SendMessageAsync("queueUrl", "body"));
        }
    }
}
