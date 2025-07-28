using Amazon.DynamoDBv2.Model;
using ClientTrackingSQSLambda.Models;
using Events.Core;
using Events.Core.Contracts;
using log4net;

namespace ClientTrackingSQSLambda.Strategies
{
    public class ClientTrackingStrategy : IEventBusMessageProcessor, IDisposable
    {
        private readonly IDynamoDbClientWrapper _dynamoDbClient;
        private readonly ILog _logger;
        private bool _alreadyDisposed = false;
        private readonly string _tableName = Environment.GetEnvironmentVariable("DYNAMODB_TABLE_NAME")
        ?? throw new InvalidOperationException("DYNAMODB_TABLE_NAME environment variable not set");
        private readonly int _partitionCount = int.Parse(Environment.GetEnvironmentVariable("CLIENT_TRACKING_PARTITION_COUNT")
            ?? throw new InvalidOperationException("CLIENT_TRACKING_PARTITION_COUNT not in environment variables"));
        private static readonly Random _exportPartitionRand = new();
        private string GetExportPartitionShard() => $"EXPORT-{_exportPartitionRand.Next(0, _partitionCount)}";

        /// <summary>
        /// Constructs a ClientTrackingStrategy with the required dependencies.
        /// </summary>
        /// <param name="dynamoDbClient">DynamoDB client wrapper for persistence.</param>
        /// <param name="logger">Logger for diagnostics and error reporting.</param>
        public ClientTrackingStrategy(IDynamoDbClientWrapper dynamoDbClient, ILog logger)
        {
            _dynamoDbClient = dynamoDbClient;
            _logger = logger;
            EventProcessor.RegisterService<ILog>(logger);
        }

        /// <summary>
        /// Processes a ClientTrackingEvent, validates required fields, applies mapping rules, and ensures unique MAC tracking.
        /// </summary>
        /// <param name="message">The event to process (should be ClientTrackingEvent).</param>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>ClientTrackingAggregatePayload with properties to update, or empty if no update is needed.</returns>
        public async Task<IEventBusProcessorResponse> ProcessEventDataAsync(IEventBusMessage message, CancellationToken cancellationToken = default)
        {
            var clientEvent = ValidateAndCastEvent(message);
            if (clientEvent == null)
            {
                return new ClientTrackingProcessorResponse
                {
                    Success = true,
                    Details = "Invalid event, treating as success",
                    Context = clientEvent
                };
            }

            var payload = BuildAggregatePayload(clientEvent);

            try
            {
                await PutDynamoRecordAsync(payload, cancellationToken);
                return new ClientTrackingProcessorResponse
                {
                    Success = true,
                    Details = "Client event saved to DynamoDB",
                    Context = payload
                };
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to save client event to DynamoDB", ex);
                return new ClientTrackingProcessorResponse
                {
                    Success = false,
                    Details = $"Error saving to DynamoDB: {ex.Message}",
                    Context = payload
                };
            }
        }

        private static ClientTrackingAggregatePayload BuildAggregatePayload(ClientTrackingEvent clientTrackingEvent)
        {
            var properties = new Dictionary<string, object>
            {
                ["Origin"] = clientTrackingEvent.Origin,
                ["Scope"] = clientTrackingEvent.Scope,
                ["DateTime"] = clientTrackingEvent.DateTime,
                ["GpnsEnabled"] = clientTrackingEvent.GpnsEnabled,
                ["SchemaName"] = clientTrackingEvent.SchemaName,
                ["SchemaVersion"] = clientTrackingEvent.SchemaVersion,
                ["IpAddress"] = clientTrackingEvent.IpAddress,
                ["MacAddress"] = clientTrackingEvent.MacAddress,
                ["UserAgentRaw"] = clientTrackingEvent.UserAgentRaw,
                ["ClientDeviceTypeId"] = clientTrackingEvent.ClientDeviceTypeId,
                ["PlatformTypeId"] = clientTrackingEvent.PlatformTypeId,
                ["BrowserTypeId"] = clientTrackingEvent.BrowserTypeId,
                ["MemberName"] = clientTrackingEvent.MemberName,
                ["MemberId"] = clientTrackingEvent.MemberId,
                ["MemberNumber"] = clientTrackingEvent.MemberNumber,
                ["ZoneType"] = clientTrackingEvent.ZoneType,
                ["Subject"] = clientTrackingEvent.Subject,
                ["ChangeType"] = clientTrackingEvent.ChangeType,
                ["AuthMethod"] = clientTrackingEvent.AuthMethod,
                ["ZonePlanName"] = clientTrackingEvent.ZonePlanName,
                ["CurrencyCode"] = clientTrackingEvent.CurrencyCode,
                ["Price"] = clientTrackingEvent.Price,
                ["TimeZoneId"] = clientTrackingEvent.TimeZoneId
            };

            return new ClientTrackingAggregatePayload
            {
                EventName = "ClientTracking",
                OrgNumber = clientTrackingEvent.OrgNumber,
                Properties = properties,
                ReadyToExportUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExportedAt = null
            };
        }

        private async Task<PutItemResponse> PutDynamoRecordAsync(ClientTrackingAggregatePayload payload, CancellationToken cancellationToken)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["EventName"] = new AttributeValue { S = payload.EventName },
                ["OrgNumber"] = new AttributeValue { S = payload.OrgNumber },
                ["ExportPartition"] = new AttributeValue { S = GetExportPartitionShard() },
                ["ReadyToExportUtc"] = new AttributeValue { N = payload.ReadyToExportUtc.ToString() },
                ["ExportedAt"] = payload.ExportedAt.HasValue
                    ? new AttributeValue { N = payload.ExportedAt.Value.ToString() }
                    : new AttributeValue { NULL = true }
            };

            // Add properties dictionary as a map
            if (payload.Properties != null && payload.Properties.Count > 0)
            {
                var propertiesMap = new Dictionary<string, AttributeValue>();
                foreach (var kvp in payload.Properties)
                {
                    propertiesMap[kvp.Key] = new AttributeValue { S = kvp.Value?.ToString() ?? string.Empty };
                }
                item["Properties"] = new AttributeValue { M = propertiesMap };
            }

            var putRequest = new PutItemRequest
            {
                TableName = _tableName,
                Item = item
            };

            return await _dynamoDbClient.PutItemAsync(putRequest, cancellationToken);
        }

        private ClientTrackingEvent? ValidateAndCastEvent(IEventBusMessage message)
        {
            if (message is not ClientTrackingEvent clientEvent)
            {
                _logger.Error("Invalid event type for ClientTrackingStrategy");
                return null;
            }
            if (string.IsNullOrWhiteSpace(clientEvent.OrgNumber))
            {
                _logger.Error(new { Message = "Missing OrgNumber in ClientTrackingEvent", ClientTrackingEvent = clientEvent });
                return null;
            }
            if (string.IsNullOrWhiteSpace(clientEvent.TimeZoneId))
            {
                _logger.Error(new { Message = "Missing TimeZoneId in ClientTrackingEvent", ClientTrackingEvent = clientEvent });
                return null;
            }
            if (string.IsNullOrWhiteSpace(clientEvent.MacAddress))
            {
                _logger.Error(new { Message = "Missing MacAddress in ClientTrackingEvent", ClientTrackingEvent = clientEvent });
                return null;
            }
            return clientEvent;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_alreadyDisposed)
            {
                return;
            }

            if (disposing)
            {
                _dynamoDbClient?.Dispose();
            }
            _alreadyDisposed = true;
        }
    }
}
