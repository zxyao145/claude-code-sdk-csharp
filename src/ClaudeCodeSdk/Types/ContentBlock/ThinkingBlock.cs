using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Thinking content block.
/// </summary>
public record ThinkingBlock : IContentBlock
{
    [JsonPropertyName("thinking")]
    public required string Thinking { get; init; }

    [JsonPropertyName("signature")]
    public required string Signature { get; init; }
}
