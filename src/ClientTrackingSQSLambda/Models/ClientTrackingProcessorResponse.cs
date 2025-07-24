using Events.Core.Contracts;

namespace ClientTrackingSQSLambda.Models
{
    public class ClientTrackingProcessorResponse : IEventBusProcessorResponse
    {
        public bool Success { get; set; } = false;
        public string Details { get; set; } = string.Empty;
        public object? Context { get; set; } = null;
    }
}
