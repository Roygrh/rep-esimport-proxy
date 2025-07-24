using Amazon.DynamoDBv2.Model;
using log4net;

namespace Events.Core.Contracts;

public interface IDynamoDbClientWrapper : IDisposable
{
    Task<UpdateItemResponse> UpdateItemAsync(UpdateItemRequest updateItemRequest, CancellationToken cancellationToken = default);
    Task<GetItemResponse> GetItemAsync(GetItemRequest getItemRequest, CancellationToken cancellationToken = default);
    Task<PutItemResponse> PutItemAsync(PutItemRequest putItemRequest, CancellationToken cancellationToken = default);
    Task<QueryResponse> QueryAsync(QueryRequest queryRequest, CancellationToken cancellationToken = default);
}
