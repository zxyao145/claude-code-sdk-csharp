using ClaudeCodeSdk.Types;
using Microsoft.Agents.AI;
using System.Diagnostics;
using System.Text.Json;

namespace ClaudeCodeSdk.MAF;

/// <summary>
/// Provides a thread implementation for use with <see cref="ClaudeCodeAIAgent"/>.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ClaudeCodeAgentThread : AgentThread
{
    /// <summary>
    /// Gets the session ID for the Claude Code conversation.
    /// </summary>
    /// <remarks>
    /// This property is set automatically when receiving the first <see cref="SystemMessage"/>
    /// from Claude Code that contains a session ID.
    /// </remarks>
    public string? SessionId { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeCodeAgentThread"/> class.
    /// </summary>
    /// <param name="sessionId">Optional session ID to resume a previous conversation.</param>
    internal ClaudeCodeAgentThread(string? sessionId = null)
    {
        SessionId = sessionId;
    }

    /// <summary>
    /// Sets the session ID if it has not been set yet, extracting it from a Claude message.
    /// </summary>
    /// <param name="claudeMessage">The message to extract the session ID from.</param>
    public void SetSessionIdIfNull(IMessage? claudeMessage)
    {
        if (claudeMessage is SystemMessage systemMessage)
        {
            SetSessionIdIfNull(systemMessage.SessionId);
        }
    }

    /// <summary>
    /// Sets the session ID if it has not been set yet.
    /// </summary>
    /// <param name="sessionId">The session ID to set.</param>
    public void SetSessionIdIfNull(string? sessionId)
    {
        if (SessionId is null && !string.IsNullOrEmpty(sessionId))
        {
            SessionId = sessionId;
        }
    }

    /// <inheritdoc/>
    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var state = new ThreadState
        {
            SessionId = SessionId
        };

        return JsonSerializer.SerializeToElement(
            state,
            jsonSerializerOptions ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay =>
        SessionId is { } sessionId
            ? $"SessionId = {sessionId}"
            : "SessionId = (not set)";

    internal sealed class ThreadState
    {
        public string? SessionId { get; set; }
    }
}
