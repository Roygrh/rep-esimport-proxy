namespace ClientTrackingSQSLambda.Models
{
    public class ClientTrackingAggregatePayload
    {
        public string EventName { get; set; } = string.Empty;
        public string OrgNumber { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = [];
        public long ReadyToExportUtc { get; set; }
        /// <summary>
        /// Unix timestamp (UTC seconds) when this row was exported to the DB. Null if not yet exported.
        /// </summary>
        public long? ExportedAt { get; set; }
    }
}
