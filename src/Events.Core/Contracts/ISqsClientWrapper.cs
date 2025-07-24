using Amazon.SQS.Model;
using Amazon.Lambda.SQSEvents;

namespace Events.Core.Contracts;

public interface ISqsClientWrapper : IDisposable
{
    Task<DeleteMessageResponse> DeleteSqsMessageAsync(SQSEvent.SQSMessage sqsRecord,
                                                      CancellationToken cancellationToken = default);
    Task<SendMessageResponse> SendMessageAsync(string queueUrl, string messageBody, CancellationToken cancellationToken = default);
}
