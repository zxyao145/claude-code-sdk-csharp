using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Tool result content block.
/// </summary>
public record ToolResultBlock : IContentBlock
{
    public string Type => "tool_result";

    [JsonPropertyName("tool_use_id")]
    public required string ToolUseId { get; init; }

    [JsonPropertyName("content")]
    public object? Content { get; init; }

    [JsonPropertyName("is_error")]
    public bool? IsError { get; init; }
}