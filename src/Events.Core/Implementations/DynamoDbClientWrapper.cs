using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using log4net;
using System.Runtime.CompilerServices;
using Events.Core.Contracts;

namespace Events.Core.Implementations;

// Wrapper for DynamoDB client operations, providing safe, testable, and logged access to DynamoDB.
public class DynamoDbClientWrapper(IAmazonDynamoDB? client = null) : IDynamoDbClientWrapper
{
    // Underlying AWS DynamoDB client
    private readonly IAmazonDynamoDB _dynamoDbClient = client ?? new AmazonDynamoDBClient();
    private bool _alreadyDisposed = false;

    /// <summary>
    /// Updates an item in DynamoDB.
    /// </summary>
    public async Task<UpdateItemResponse> UpdateItemAsync(UpdateItemRequest updateItemRequest,
                                                CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.UpdateItemAsync(updateItemRequest, cancellationToken);
    }

    /// <summary>
    /// Gets an item from DynamoDB.
    /// </summary>
    public async Task<GetItemResponse> GetItemAsync(GetItemRequest getItemRequest, CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.GetItemAsync(getItemRequest, cancellationToken);
    }
    /// <summary>
    /// Puts an item into DynamoDB.
    /// </summary>
    public async Task<PutItemResponse> PutItemAsync(PutItemRequest putItemRequest, CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.PutItemAsync(putItemRequest, cancellationToken);
    }

    /// <summary>
    /// Queries a DynamoDB table using a specified index.
    /// </summary>
    public async Task<QueryResponse> QueryAsync(QueryRequest queryRequest, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_alreadyDisposed, this);
        return await _dynamoDbClient.QueryAsync(queryRequest, cancellationToken);
    }

    /// <summary>
    /// Disposes the underlying DynamoDB client.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_alreadyDisposed)
        {
            if (disposing)
            {
                _dynamoDbClient.Dispose();
            }
            _alreadyDisposed = true;
        }
    }

}
