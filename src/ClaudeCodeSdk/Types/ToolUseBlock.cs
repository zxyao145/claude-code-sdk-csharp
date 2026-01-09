using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Tool use content block.
/// </summary>
public record ToolUseBlock : IContentBlock
{
    public string Type => "tool_use";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("input")]
    public required Dictionary<string, object> Input { get; init; }
}