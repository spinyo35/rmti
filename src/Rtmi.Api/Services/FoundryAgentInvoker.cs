using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Core;
using OpenAI;
using OpenAI.Responses;

namespace Rtmi.Api.Services;

#pragma warning disable OPENAI001
public sealed class FoundryAgentInvoker(IConfiguration configuration, TokenCredential credential) : IFoundryAgentInvoker
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Foundry:Endpoint"]);

    public Task<string> InvokeTextAsync(
        string agentName,
        string input,
        CancellationToken cancellationToken) =>
        InvokeAsync(agentName, [ResponseItem.CreateUserMessageItem(input)], cancellationToken);

    public Task<string> InvokeWithImagesAsync(
        string agentName,
        string input,
        IReadOnlyList<string> imageDataUrls,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageDataUrls);
        var content = new List<ResponseContentPart>
        {
            ResponseContentPart.CreateInputTextPart(input)
        };
        content.AddRange(imageDataUrls.Select(dataUrl =>
            ResponseContentPart.CreateInputImagePart(new Uri(dataUrl, UriKind.Absolute))));
        return InvokeAsync(agentName, [ResponseItem.CreateUserMessageItem(content)], cancellationToken);
    }

    private async Task<string> InvokeAsync(
        string agentName,
        IReadOnlyList<ResponseItem> inputItems,
        CancellationToken cancellationToken)
    {
        var endpointValue = configuration["Foundry:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpointValue))
            throw new InvalidOperationException("Foundry:Endpoint is not configured.");
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("Foundry:Endpoint must be an absolute URI.");
        if (string.IsNullOrWhiteSpace(agentName))
            throw new ArgumentException("An agent name is required.", nameof(agentName));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = Math.Clamp(configuration.GetValue("Foundry:TimeoutSeconds", 45), 1, 120);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        AIProjectClient projectClient = new(
            endpoint: endpoint,
            tokenProvider: credential);
        ProjectResponsesClient agentClient =
            projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgentEndpoint(agentName);
        ProjectConversationsClient conversationClient = new(
            agentClient.Pipeline,
            new OpenAIClientOptions { Endpoint = agentClient.Endpoint });
        ProjectConversation conversation =
            await conversationClient.CreateProjectConversationAsync(cancellationToken: timeout.Token);

        ProjectResponsesClient responseClient =
            projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgentEndpoint(
                agentName,
                defaultConversationId: conversation.Id);
        ResponseResult response =
            await responseClient.CreateResponseAsync(inputItems, cancellationToken: timeout.Token);
        var output = response.GetOutputText();
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException($"Foundry agent '{agentName}' returned an empty response.");

        return output;
    }
}
#pragma warning restore OPENAI001
