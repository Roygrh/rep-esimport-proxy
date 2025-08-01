using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Events.Core.Contracts;
using log4net;

namespace Events.Core.Implementations;

// Wrapper for AWS SSM client operations, providing safe, testable, and logged access to SSM.
public partial class SsmClientWrapper : ISsmClientWrapper
{
    // Underlying AWS SSM client
    private readonly IAmazonSimpleSystemsManagement _ssmClient;
    private bool _alreadyDisposed = false;
    private readonly ILog _logger;

    /// <summary>
    /// Constructs a new SsmClientWrapper with optional injected AWS client and logger.
    /// </summary>
    public SsmClientWrapper(IAmazonSimpleSystemsManagement? ssmClient = null, ILog? logger = null)
    {
        ObjectDisposedException.ThrowIf(_alreadyDisposed, nameof(SsmClientWrapper));
        _ssmClient = ssmClient ?? new AmazonSimpleSystemsManagementClient();
        _logger = logger ?? LogManager.GetLogger("Reporting.Portal.Metrics.Core.SSMClientService");
    }

    /// <summary>
    /// Gets a string parameter from SSM Parameter Store. Returns an empty string on error to avoid null reference issues.
    /// </summary>
    public async Task<string> GetStringParameterAsync(string parameterName, bool withDecryption = false)
    {
        ObjectDisposedException.ThrowIf(_alreadyDisposed, nameof(SsmClientWrapper));
        try
        {
            var response = await _ssmClient.GetParameterAsync(new GetParameterRequest
            {
                Name = parameterName,
                WithDecryption = withDecryption
            });

            return response.Parameter.Value;
        }
        catch (Exception ex)
        {
            _logger.Error(new
            {
                Message = "Exception occured in GetStringParameterAsync",
                ParameterName = parameterName,
                WithDecryption = withDecryption
            }, ex);
            return string.Empty; // Return empty string on error to avoid null reference issues
        }
    }

    /// <summary>
    /// Sends a command to an EC2 instance using SSM.
    /// </summary>
    public async Task<SendCommandResponse> SendCommandAsync(SendCommandRequest sendCommandRequest, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_alreadyDisposed, nameof(SsmClientWrapper));
        try
        {
            return await _ssmClient.SendCommandAsync(sendCommandRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(new
            {
                Message = "Exception occurred in SendCommandAsync",
                Command = sendCommandRequest?.DocumentName,
                sendCommandRequest?.InstanceIds
            }, ex);
            return new SendCommandResponse
            {
                HttpStatusCode = System.Net.HttpStatusCode.InternalServerError,
            };
        }
    }

    /// <summary>
    /// Retrieves the details of a command invocation, including the command's status and output.
    /// </summary>
    public async Task<GetCommandInvocationResponse?> GetCommandInvocationAsync(GetCommandInvocationRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_alreadyDisposed, nameof(SsmClientWrapper));
        try
        {
            return await _ssmClient.GetCommandInvocationAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(new
            {
                Message = "Exception occurred in GetCommandInvocationAsync",
                request?.CommandId,
                request?.InstanceId
            }, ex);
            return null;
        }
    }

    /// <summary>
    /// Disposes the underlying SSM client.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_alreadyDisposed)
        {
            if (disposing)
            {
                _ssmClient.Dispose();
            }
            _alreadyDisposed = true;
        }
    }
}
