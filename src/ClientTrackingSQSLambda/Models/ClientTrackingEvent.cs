using ClientTrackingSQSLambda.Strategies;
using Events.Core.Contracts;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientTrackingSQSLambda.Models
{
    public class ClientTrackingEvent : IEventBusMessage
    {
        public string Origin { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public DateTime DateTime { get; set; } = DateTime.UtcNow;
        public bool GpnsEnabled { get; set; } = false;
        public string SchemaName { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string UserAgentRaw { get; set; } = string.Empty;
        public string ClientDeviceTypeId { get; set; } = string.Empty;
        public string PlatformTypeId { get; set; } = string.Empty;
        public string BrowserTypeId { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public int MemberId { get; set; } = 0;
        public string MemberNumber { get; set; } = string.Empty;
        public string OrgNumber { get; set; } = string.Empty;
        public string ZoneType { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty;
        public string AuthMethod { get; set; } = string.Empty;
        public string ZonePlanName { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public double Price { get; set; }
        public string TimeZoneId { get; set; } = string.Empty;

        public IEventBusMessageProcessor GetEventProcessor(IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var dynamoDB = (IDynamoDbClientWrapper?)serviceProvider.GetService(typeof(IDynamoDbClientWrapper));
            var logger = (ILog?)serviceProvider.GetService(typeof(ILog));

            ArgumentNullException.ThrowIfNull(dynamoDB);
            ArgumentNullException.ThrowIfNull(logger);

            return new ClientTrackingStrategy(dynamoDB, logger);
        }
    }
}
