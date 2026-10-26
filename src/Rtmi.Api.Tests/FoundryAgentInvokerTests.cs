using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Rtmi.Api.Services;

namespace Rtmi.Api.Tests;

public sealed class FoundryAgentInvokerTests
{
    [Fact]
    public async Task DoesNotAttemptAnInvocationWithoutAnEndpoint()
    {
        var configuration = new ConfigurationBuilder().Build();
        var invoker = new FoundryAgentInvoker(configuration, new AzureCliCredential(), NullLogger<FoundryAgentInvoker>.Instance);

        Assert.False(invoker.IsConfigured);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeTextAsync("rtmi-narrator", "{}", CancellationToken.None));
        Assert.Equal("Foundry:Endpoint is not configured.", exception.Message);
    }
}
