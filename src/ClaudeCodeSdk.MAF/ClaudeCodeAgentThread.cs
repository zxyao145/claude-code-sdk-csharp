using ClaudeCodeSdk.Types;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ClaudeCodeSdk.MAF;

/// <summary>
/// AgentThread implementation for ClaudeCode conversations.
/// </summary>
internal class ClaudeCodeAgentThread : AgentThread
{
    public string? SessionId { get; private set; }

    public ClaudeCodeAgentThread(string? sessionId = null)
    {
        SessionId = sessionId;
    }

    public void SetSessionIdIfNull(IMessage? claudeMessage)
    {
        if (claudeMessage == null)
        {
            return;
        }
        if (claudeMessage is SystemMessage systemMessage)
        {
            SetSessionIdIfNull(systemMessage.SessionId);
        }
    }

    public void SetSessionIdIfNull(string sessionId)
    {
        if (SessionId == null)
        {
            SessionId = sessionId;
        }
    }
}