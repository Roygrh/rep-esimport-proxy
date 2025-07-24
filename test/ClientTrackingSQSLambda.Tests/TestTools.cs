using ClientTrackingSQSLambda.Models;

namespace ClientTrackingSQSLambda.Tests;

public class TestTools
{
    readonly Random rand = new();

    public List<string> _deviceTypes =
    [
        "Laptop",
        "Tablet",
        "Smartphone",
        "GameConsole",
        "Other"
    ];

    public List<string> _platformTypes =
    [
        "Windows",
        "Mac",
        "Ios",
        "Android",
        "Other"
    ];

    public List<string> _browserTypes =
    [
        "Chrome",
        "Firefox",
        "Safari",
        "Edge",
        "Opera"
    ];

    public List<string> _zoneTypes =
    [
        "Guest",
        "Public",
        "Meeting"
    ];

    public ClientTrackingEvent GetRandomClientTrackingEvent()
    {
        return new ClientTrackingEvent
        {
            Origin = "TestOrigin",
            Scope = "TestScope",
            DateTime = DateTime.UtcNow,
            SchemaName = "TestSchema",
            SchemaVersion = "1.0",
            IpAddress = $"192.168.{rand.Next(0, 255)}.{rand.Next(0, 255)}",
            MacAddress = $"{rand.Next(0, 255):X2}:{rand.Next(0, 255):X2}:{rand.Next(0, 255):X2}:{rand.Next(0, 255):X2}:{rand.Next(0, 255):X2}:{rand.Next(0, 255):X2}",
            UserAgentRaw = "Mozilla/5.0 (TestAgent)",
            ClientDeviceTypeId = _deviceTypes[rand.Next(_deviceTypes.Count)],
            PlatformTypeId = _platformTypes[rand.Next(_platformTypes.Count)],
            BrowserTypeId = _browserTypes[rand.Next(_browserTypes.Count)],
            MemberName = $"TestUser{rand.Next(1, 1000)}",
            MemberId = rand.Next(1, 10000),
            MemberNumber = $"M{rand.Next(1000, 9999)}",
            OrgNumber = $"O{rand.Next(100, 999)}",
            ZoneType = _zoneTypes[rand.Next(_zoneTypes.Count)],
            Subject = "ClientTracking",
            ChangeType = "TestChange",
            TimeZoneId = "Europe/Stockholm"
        };
    }
}

