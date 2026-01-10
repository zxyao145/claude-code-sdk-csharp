using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// System message with metadata.
/// </summary>
public record SystemMessage : IMessage
{
    public string Id { get; init; } = "";

    public MessageType Type => MessageType.System;

    [JsonPropertyName("subtype")]
    public required string Subtype { get; init; }

    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    [JsonPropertyName("data")]
    public required Dictionary<string, object> Data { get; init; }
}