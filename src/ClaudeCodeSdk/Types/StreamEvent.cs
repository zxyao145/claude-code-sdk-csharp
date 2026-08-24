using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Represents a raw partial-message event emitted by the Claude Code CLI.
/// </summary>
public record StreamEvent : IMessage
{
    public string Id { get; init; } = "";

    public MessageType Type => MessageType.StreamEvent;

    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    [JsonPropertyName("parent_tool_use_id")]
    public string? ParentToolUseId { get; init; }

    [JsonPropertyName("event")]
    public required JsonElement Event { get; init; }
}
