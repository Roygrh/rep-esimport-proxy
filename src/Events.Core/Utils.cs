using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Events.Core.Contracts;
using log4net;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Events.Core;

public static partial class Utils
{
    // Regex to fix legacy JSON escaping issues in SQS payloads
    // This regex matches a backslash followed by a double quote, which is an incorrect escape sequence
    // and replaces it with just the double quote.
    [GeneratedRegex(@"\\(?="")")]
    private static partial Regex MyRegex();
    
    /// <summary>
    /// Deserializes an SQS message body to the expected event type, handling polymorphic event types.
    /// </summary>
    /// <typeparam name="T">The expected event type (must implement IEventBusMessage).</typeparam>
    /// <param name="message">The SQS message body as a JSON string.</param>
    /// <returns>The deserialized event object, or null if deserialization fails.</returns>
    public static T? DeserializeSafe<T>(this string message, ILog? logger = null) where T : class
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            if (typeof(T) == typeof(IEventBusMessage))
            {
                options.Converters.Add(new Json.EventBusJsonConverter());
            }
            if (typeof(T) == typeof(object))
            {
                // Deserialize to ExpandoObject for dynamic property access
                return JsonSerializer.Deserialize<System.Dynamic.ExpandoObject>(message, options) as T;
            }
            return JsonSerializer.Deserialize<T>(message, options);
        }
        catch (JsonException ex)
        {
            //log exception if needed
            logger?.Error(new
            {
                Message = "Failed to deserialize message",
                MessageContent = message
            }, ex);
            return default;
        }
    }
    /// <summary>
    /// Cleans up JSON escaping for SQS payloads (legacy compatibility).
    /// </summary>
    /// <param name="inputJson">The input JSON string with potential escaping issues.</param>
    /// <returns>The corrected JSON string with proper escaping.</returns>
    public static string CorrectJsonEscaping(this string inputJson)
    {
        // Step 1: Replace Unicode-encoded quotes (\u0022) with actual double quotes
        var correctedJson = inputJson.Replace(@"\u0022", "\"");

        // Step 2: Replace overly escaped backslashes specifically around quotes
        correctedJson = MyRegex().Replace(correctedJson, "");

        // Step 3: Decode remaining escaped quotes and backslashes in a controlled way
        correctedJson = correctedJson.Replace("\\\"", "\"").Replace("\\\\", "\\");

        // Handle escaped forward slashes (legacy compatibility)
        correctedJson = correctedJson.Replace("\\/", "/");

        // Step 4: Remove surrounding double quotes if they exist
        if (correctedJson.StartsWith('"') && correctedJson.EndsWith('"'))
        {
            correctedJson = correctedJson[1..^1];
        }

        return correctedJson;
    }
    /// <summary>
    /// Creates a <see cref="CancellationToken"> that is canceled when the Lambda function is about to time out.
    /// </summary>
    /// <param name="context">The <see cref="ILambdaContext"> instance providing the remaining execution time for the Lambda function.</param>
    /// <returns>A <see cref="CancellationToken"> that will be canceled when the remaining execution time of the Lambda function
    /// expires.</returns>
    public static CancellationToken GetCTSToken(this ILambdaContext context)
    {
        // Create a cancellation token that will cancel when the Lambda function is about to timeout
        return new CancellationTokenSource(context.RemainingTime).Token;
    }
}
