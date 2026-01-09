using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Assistant message with content blocks.
/// </summary>
public record AssistantMessage : IMessage
{
    public MessageType Type => MessageType.Assistant;

    [JsonPropertyName("content")]
    public required IReadOnlyList<IContentBlock> Content { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }
}