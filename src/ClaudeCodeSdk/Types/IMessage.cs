using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Base interface for messages.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AssistantMessage), "assistant")]
[JsonDerivedType(typeof(UserMessage), "user")]
[JsonDerivedType(typeof(SystemMessage), "system")]
[JsonDerivedType(typeof(ResultMessage), "result")]
public interface IMessage
{
    MessageType Type { get; }
    string Id { get; }
}
