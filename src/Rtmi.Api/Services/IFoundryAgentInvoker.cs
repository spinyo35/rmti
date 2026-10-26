namespace Rtmi.Api.Services;

public interface IFoundryAgentInvoker
{
    bool IsConfigured { get; }

    Task<string> InvokeTextAsync(string agentName, string input, CancellationToken cancellationToken);

    Task<string> InvokeWithImagesAsync(
        string agentName,
        string input,
        IReadOnlyList<string> imageDataUrls,
        CancellationToken cancellationToken);
}
