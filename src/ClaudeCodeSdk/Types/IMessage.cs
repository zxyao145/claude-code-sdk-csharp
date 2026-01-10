namespace ClaudeCodeSdk.Types;

/// <summary>
/// Base interface for messages.
/// </summary>
public interface IMessage
{
    MessageType Type { get; }
    string Id { get; }
}