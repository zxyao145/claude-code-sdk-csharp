using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// User message.
/// </summary>
public record UserMessage : IMessage
{
    public string Type => "user";

    [JsonPropertyName("content")]
    public required object Content { get; init; } // string or List<IContentBlock>
}