using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// MCP SSE server configuration.
/// </summary>
public record McpSSEServerConfig : IMcpServerConfig
{
    [JsonPropertyName("type")]
    public string Type => "sse";

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("headers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}
