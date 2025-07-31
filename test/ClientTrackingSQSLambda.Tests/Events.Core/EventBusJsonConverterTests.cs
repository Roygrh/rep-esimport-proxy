using ClientTrackingSQSLambda.Models;
using Events.Core.Contracts;
using Events.Core.Json;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClientTrackingSQSLambda.Tests.Events.Core
{
    [TestFixture]
    public class EventBusJsonConverterTests
    {
        private JsonSerializerOptions _options;

        [SetUp]
        public void Setup()
        {
            _options = new JsonSerializerOptions
            {
                Converters = { new EventBusJsonConverter() }
            };
            EventBusJsonConverter.RegisterEventType("ClientTracking", typeof(ClientTrackingEvent));
        }

        [Test]
        public void Read_ThrowsIfSubjectMissing()
        {
            var json = @"{ ""Origin"": ""origin"" }";
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<IEventBusMessage>(json, _options));
        }

        [Test]
        public void Read_ThrowsIfSubjectUnknown()
        {
            var json = @"{ ""Subject"": ""Unknown"", ""Origin"": ""origin"" }";
            var ex = Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<IEventBusMessage>(json, _options));
            StringAssert.Contains("Unknown Subject type", ex.Message);
        }

        [Test]
        public void Read_DeserializesKnownType()
        {
            var json = @"{
                ""Subject"": ""ClientTracking"",
                ""Origin"": ""origin"",
                ""Scope"": ""scope"",
                ""ChangeType"": ""change"",
                ""DateTime"": ""2024-01-01T00:00:00Z"",
                ""SchemaName"": ""schema"",
                ""SchemaVersion"": ""1.0""
            }";
            var result = JsonSerializer.Deserialize<IEventBusMessage>(json, _options);
            Assert.That(result, Is.TypeOf<ClientTrackingEvent>());
            Assert.That(result.Subject, Is.EqualTo("ClientTracking"));
        }

        [Test]
        public void Write_SerializesConcreteType()
        {
            var dummy = new ClientTrackingEvent
            {
                Origin = "origin",
                Subject = "ClientTracking",
                Scope = "scope",
                ChangeType = "change",
                DateTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                SchemaName = "schema",
                SchemaVersion = "1.0"
            };
            var json = JsonSerializer.Serialize<IEventBusMessage>(dummy, _options);
            StringAssert.Contains(@"""Subject"":""ClientTracking""", json);
            StringAssert.Contains(@"""Origin"":""origin""", json);
        }
    }
}
