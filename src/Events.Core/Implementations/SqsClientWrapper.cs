using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.Lambda.SQSEvents;
using Events.Core.Contracts;

namespace Events.Core.Implementations;

/// <summary>
/// SQS client wrapper for deleting SQS messages. Implements IDisposable for resource cleanup.
/// </summary>
public class SqsClientWrapper(IAmazonSQS? client = null) : ISqsClientWrapper
{
    private readonly IAmazonSQS _sqsClient = client ?? new AmazonSQSClient();
    private bool _alreadyDisposed = false;


    /// <summary>
    /// Deletes an SQS message using the event source ARN and receipt handle.
    /// </summary>
    public async Task<DeleteMessageResponse> DeleteSqsMessageAsync(SQSEvent.SQSMessage sqsRecord,
                                                                   CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_alreadyDisposed, nameof(SqsClientWrapper));

        var arnParts = sqsRecord.EventSourceArn.Split(':');
        var accountId = arnParts.Length > 4
            ? arnParts[4]
            : throw new InvalidOperationException("Could not determine accountID from Arn");
        var queueName = arnParts[^1]; // Use index instead of .Last() for SonarCloud compliance
        var queueUrl = $"https://sqs.{sqsRecord.AwsRegion}.amazonaws.com/{accountId}/{queueName}";
        return await DeleteMessageAsync(queueUrl, sqsRecord.ReceiptHandle, cancellationToken);
    }


    /// <summary>
    /// Deletes an SQS message by queue URL and receipt handle.
    /// </summary>
    private async Task<DeleteMessageResponse> DeleteMessageAsync(string queueUrl, string receiptHandle,
                                                         CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_alreadyDisposed, nameof(SqsClientWrapper));
        return await _sqsClient.DeleteMessageAsync(queueUrl, receiptHandle, cancellationToken);
    }


    /// <summary>
    /// Sends a message to the specified SQS queue URL.
    /// </summary>
    public async Task<SendMessageResponse> SendMessageAsync(string queueUrl, string messageBody, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_alreadyDisposed, nameof(SqsClientWrapper));
        var req = new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = messageBody
        };
        return await _sqsClient.SendMessageAsync(req, cancellationToken);
    }


    /// <summary>
    /// Disposes the SQS client and releases resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }


    /// <summary>
    /// Protected dispose pattern implementation.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_alreadyDisposed)
        {
            if (disposing)
            {
                _sqsClient.Dispose();
            }
            _alreadyDisposed = true;
        }
    }
}
