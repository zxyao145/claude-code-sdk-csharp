using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Assistant message with content blocks.
/// </summary>
public record AssistantMessage : IMessage
{
    public string Type => "assistant";
    
    [JsonPropertyName("content")]
    public required IReadOnlyList<IContentBlock> Content { get; init; }
    
    [JsonPropertyName("model")]
    public required string Model { get; init; }
}
