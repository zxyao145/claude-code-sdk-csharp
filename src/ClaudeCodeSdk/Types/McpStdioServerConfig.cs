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
    public IReadOnlyList<string>? Args { get; init; }
    
    [JsonPropertyName("env")]
    public IReadOnlyDictionary<string, string>? Environment { get; init; }
}
