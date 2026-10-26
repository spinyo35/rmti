using System.Text.Json;
using Rtmi.Api.Services;

namespace Rtmi.Api.Tests;

public sealed class AgentJsonResponseParserTests
{
    [Fact]
    public void ParsesSingleJsonObjectCaseInsensitively()
    {
        var result = AgentJsonResponseParser.Parse<AgentOutput>(
            """{"text":"A grounded line.","why":"evt_0001 supports it.","evidenceEventIds":["evt_0001"],"confidence":0.8}""");

        Assert.Equal("A grounded line.", result.Text);
        Assert.Equal(["evt_0001"], result.EvidenceEventIds);
        Assert.Equal(0.8, result.Confidence);
    }

    [Theory]
    [InlineData("```json\n{\"text\":\"wrapped\"}\n```")]
    [InlineData("[{\"text\":\"not an object\"}]")]
    [InlineData("{\"text\":\"ok\",\"extra\":true}")]
    public void RejectsNonObjectWrappedOrUnexpectedAgentOutput(string output)
    {
        Assert.ThrowsAny<JsonException>(() => AgentJsonResponseParser.Parse<AgentOutput>(output));
    }

    private sealed class AgentOutput
    {
        public string? Text { get; init; }
        public string? Why { get; init; }
        public List<string>? EvidenceEventIds { get; init; }
        public double Confidence { get; init; }
    }
}
