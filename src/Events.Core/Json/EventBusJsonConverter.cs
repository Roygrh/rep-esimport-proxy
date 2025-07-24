using System.Text.Json;
using System.Text.Json.Serialization;
using Events.Core.Contracts;

namespace Events.Core.Json;

/// <summary>
/// Polymorphic JSON converter for IEventBusMessage. Deserializes based on the Subject property.
/// </summary>
public class EventBusJsonConverter : JsonConverter<IEventBusMessage>
{
    // Registry for subject string to event type
    private static readonly Dictionary<string, Type> _typeRegistry = [];

    /// <summary>
    /// Registers an event type for a given subject string.
    /// Should be called by feature assemblies at startup.
    /// </summary>
    public static void RegisterEventType(string subject, Type type)
    {
        _typeRegistry[subject] = type;
    }

    /// <summary>
    /// Reads and deserializes the event based on the Subject property.
    /// </summary>
    public override IEventBusMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);

        var root = jsonDoc.RootElement;
        if (!root.TryGetProperty("Subject", out var subjectProp))
        {
            throw new JsonException("Missing Subject property for IEventBusMessage polymorphic deserialization");
        }

        var subject = subjectProp.GetString();
        if (subject != null && _typeRegistry.TryGetValue(subject, out var type))
        {
            return (IEventBusMessage?)JsonSerializer.Deserialize(root.GetRawText(), type, options);
        }
        throw new JsonException($"Unknown Subject type: {subject}");
    }

    /// <summary>
    /// Serializes the event using its concrete type.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, IEventBusMessage value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
