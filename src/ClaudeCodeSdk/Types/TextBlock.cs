using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Text content block.
/// </summary>
public record TextBlock : IContentBlock
{
    public string Type => "text";

    [JsonPropertyName("text")]
    public required string Text { get; init; }
}