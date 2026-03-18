using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Text content block.
/// </summary>
public record TextBlock : ContentBlockBase, IContentBlock
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}
