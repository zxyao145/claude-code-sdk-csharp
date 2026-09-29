using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// MCP stdio server configuration.
/// </summary>
public record McpStdioServerConfig : IMcpServerConfig
{
    [JsonPropertyName("type")]
    public string Type => "stdio";

    [JsonPropertyName("command")]
    public required string Command { get; init; }

    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Args { get; init; }

    [JsonPropertyName("env")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Environment { get; init; }
}
