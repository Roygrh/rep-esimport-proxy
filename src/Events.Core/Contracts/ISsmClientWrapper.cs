using Amazon.SimpleSystemsManagement.Model;

namespace Events.Core.Contracts;

public interface ISsmClientWrapper : IDisposable
{
    Task<string> GetStringParameterAsync(string parameterName, bool withDecryption = false);
    Task<SendCommandResponse> SendCommandAsync(SendCommandRequest sendCommandRequest, CancellationToken cancellationToken = default);
    Task<GetCommandInvocationResponse?> GetCommandInvocationAsync(GetCommandInvocationRequest request, CancellationToken cancellationToken = default);
}
