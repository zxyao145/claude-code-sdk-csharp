using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Assistant message with content blocks.
/// </summary>
public record AssistantMessage : IMessage
{
    public string Id { get; init; } = "";

    public MessageType Type => MessageType.Assistant;

    [JsonPropertyName("content")]
    public required IReadOnlyList<IContentBlock> Content { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the provider message ID from the nested Claude API message.
    /// </summary>
    public string? ApiMessageId { get; init; }

    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }
}
