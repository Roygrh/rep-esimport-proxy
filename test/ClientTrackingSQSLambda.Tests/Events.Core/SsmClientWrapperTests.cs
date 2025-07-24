using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Events.Core.Implementations;
using log4net;
using Moq;
using NUnit.Framework;

namespace ClientTrackingSQSLambda.Tests.Events.Core
{
    [TestFixture]
    public class SsmClientWrapperTests
    {
        [Test]
        public async Task GetStringParameterAsync_ReturnsValue()
        {
            var mockSsm = new Mock<IAmazonSimpleSystemsManagement>();
            var mockLogger = new Mock<ILog>();
            mockSsm.Setup(s => s.GetParameterAsync(It.IsAny<GetParameterRequest>(), default))
                .ReturnsAsync(new GetParameterResponse { Parameter = new Parameter { Value = "foo" } });
            var wrapper = new SsmClientWrapper(mockSsm.Object, mockLogger.Object);
            var result = await wrapper.GetStringParameterAsync("param");
            Assert.That(result, Is.EqualTo("foo"));
        }

        [Test]
        public async Task GetStringParameterAsync_Throws_LogsAndReturnsEmptyString()
        {
            var mockSsm = new Mock<IAmazonSimpleSystemsManagement>();
            var mockLogger = new Mock<ILog>();
            mockSsm.Setup(s => s.GetParameterAsync(It.IsAny<GetParameterRequest>(), default))
                .ThrowsAsync(new Exception("fail"));
            var wrapper = new SsmClientWrapper(mockSsm.Object, mockLogger.Object);
            var result = await wrapper.GetStringParameterAsync("param");
            mockLogger.Verify(l => l.Error(It.IsAny<object>(), It.IsAny<Exception>()), Times.Once);
            Assert.That(result, Is.EqualTo(string.Empty));
        }


        [Test]
        public void Dispose_MultipleCalls_DoesNotThrow()
        {
            var wrapper = new SsmClientWrapper();
            wrapper.Dispose();
            Assert.DoesNotThrow(() => wrapper.Dispose());
        }

        [Test]
        public async Task SendCommandAsync_Success_ReturnsResponse()
        {
            var mockSsm = new Mock<IAmazonSimpleSystemsManagement>();
            var mockLogger = new Mock<ILog>();
            var expectedResponse = new SendCommandResponse { HttpStatusCode = System.Net.HttpStatusCode.OK };
            mockSsm.Setup(s => s.SendCommandAsync(It.IsAny<SendCommandRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var wrapper = new SsmClientWrapper(mockSsm.Object, mockLogger.Object);
            var request = new SendCommandRequest { DocumentName = "doc", InstanceIds = new List<string> { "i-123" } };
            var result = await wrapper.SendCommandAsync(request);

            Assert.That(result, Is.EqualTo(expectedResponse));
        }

        [Test]
        public async Task SendCommandAsync_Exception_LogsAndReturnsInternalServerError()
        {
            var mockSsm = new Mock<IAmazonSimpleSystemsManagement>();
            var mockLogger = new Mock<ILog>();
            mockSsm.Setup(s => s.SendCommandAsync(It.IsAny<SendCommandRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("fail"));

            var wrapper = new SsmClientWrapper(mockSsm.Object, mockLogger.Object);
            var request = new SendCommandRequest { DocumentName = "doc", InstanceIds = new List<string> { "i-123" } };
            var result = await wrapper.SendCommandAsync(request);

            mockLogger.Verify(l => l.Error(It.IsAny<object>(), It.IsAny<Exception>()), Times.Once);
            Assert.That(result.HttpStatusCode, Is.EqualTo(System.Net.HttpStatusCode.InternalServerError));
        }

        [Test]
        public async Task GetCommandInvocationAsync_Success_ReturnsResponse()
        {
            var mockSsm = new Mock<IAmazonSimpleSystemsManagement>();
            var mockLogger = new Mock<ILog>();
            var expectedResponse = new GetCommandInvocationResponse { Status = CommandInvocationStatus.Success };
            mockSsm.Setup(s => s.GetCommandInvocationAsync(It.IsAny<GetCommandInvocationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var wrapper = new SsmClientWrapper(mockSsm.Object, mockLogger.Object);
            var request = new GetCommandInvocationRequest { CommandId = "cmd-123", InstanceId = "i-123" };
            var result = await wrapper.GetCommandInvocationAsync(request);

            Assert.That(result, Is.EqualTo(expectedResponse));
        }

        [Test]
        public async Task GetCommandInvocationAsync_Exception_LogsAndReturnsNull()
        {
            var mockSsm = new Mock<IAmazonSimpleSystemsManagement>();
            var mockLogger = new Mock<ILog>();
            mockSsm.Setup(s => s.GetCommandInvocationAsync(It.IsAny<GetCommandInvocationRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("fail"));

            var wrapper = new SsmClientWrapper(mockSsm.Object, mockLogger.Object);
            var request = new GetCommandInvocationRequest { CommandId = "cmd-123", InstanceId = "i-123" };
            var result = await wrapper.GetCommandInvocationAsync(request);

            mockLogger.Verify(l => l.Error(It.IsAny<object>(), It.IsAny<Exception>()), Times.Once);
            Assert.That(result, Is.Null);
        }
    }


}
