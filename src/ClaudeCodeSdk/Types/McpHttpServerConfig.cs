using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// MCP HTTP server configuration.
/// </summary>
public record McpHttpServerConfig : IMcpServerConfig
{
    [JsonPropertyName("type")]
    public string Type => "http";

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("headers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}
