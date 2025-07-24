using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Events.Core.Implementations;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientTrackingSQSLambda.Tests.Events.Core
{

    [TestFixture]
    public class DynamoDbClientWrapperTests
    {
        [Test]
        public async Task UpdateItemAsync_CallsUnderlyingClient()
        {
            var mockDdb = new Mock<IAmazonDynamoDB>();
            mockDdb.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateItemResponse());
            var wrapper = new DynamoDbClientWrapper(mockDdb.Object);
            var resp = await wrapper.UpdateItemAsync(new UpdateItemRequest());
            Assert.That(resp, Is.TypeOf<UpdateItemResponse>());
        }

        [Test]
        public async Task GetItemAsync_CallsUnderlyingClient()
        {
            var mockDdb = new Mock<IAmazonDynamoDB>();
            mockDdb.Setup(d => d.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetItemResponse());
            var wrapper = new DynamoDbClientWrapper(mockDdb.Object);
            var resp = await wrapper.GetItemAsync(new GetItemRequest());
            Assert.That(resp, Is.TypeOf<GetItemResponse>());
        }

        [Test]
        public async Task PutItemAsync_CallsUnderlyingClient()
        {
            var mockDdb = new Mock<IAmazonDynamoDB>();
            mockDdb.Setup(d => d.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PutItemResponse());
            var wrapper = new DynamoDbClientWrapper(mockDdb.Object);
            var resp = await wrapper.PutItemAsync(new PutItemRequest());
            Assert.That(resp, Is.TypeOf<PutItemResponse>());
        }

        [Test]
        public void Dispose_MultipleCalls_DoesNotThrow()
        {
            var wrapper = new DynamoDbClientWrapper();
            wrapper.Dispose();
            Assert.DoesNotThrow(() => wrapper.Dispose());
        }
    }
}
