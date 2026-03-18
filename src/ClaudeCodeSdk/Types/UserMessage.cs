using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// User message.
/// </summary>
public record UserMessage : IMessage
{
    [JsonPropertyName("uuid")]
    public string Id { get; init; } = "";

    [JsonIgnore]
    public MessageType Type => MessageType.User;

    [JsonPropertyName("content")]
    [JsonConverter(typeof(UserMessageContentJsonConverter))]
    public required object Content { get; init; } // string or List<IContentBlock>
}
