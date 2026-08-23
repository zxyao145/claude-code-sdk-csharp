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

    /// <summary>
    /// Gets the optional message-level tool result metadata in its original JSON shape.
    /// </summary>
    [JsonPropertyName("tool_use_result")]
    public object? ToolUseResult { get; init; }

    [JsonPropertyName("is_error")]
    public bool? IsError { get; init; }
}
