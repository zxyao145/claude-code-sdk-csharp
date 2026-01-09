using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ClaudeCodeSdk.MAF;

/// <summary>
/// AgentThread implementation for ClaudeCode conversations.
/// </summary>
internal class ClaudeCodeAgentThread : AgentThread
{
    public string SessionId { get; }
    public List<ChatMessage> Messages { get; }

    public ClaudeCodeAgentThread(string? sessionId = null, List<ChatMessage>? messages = null)
    {
        SessionId = sessionId ?? Guid.NewGuid().ToString();
        Messages = messages ?? new List<ChatMessage>();
    }
}