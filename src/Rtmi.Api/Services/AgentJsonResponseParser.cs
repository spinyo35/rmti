using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rtmi.Api.Services;

public static class AgentJsonResponseParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static T Parse<T>(string output)
    {
        using var document = JsonDocument.Parse(output);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("Agent output must be a single JSON object.");

        return JsonSerializer.Deserialize<T>(document.RootElement, Options)
            ?? throw new JsonException("Agent output did not contain a valid response object.");
    }
}
