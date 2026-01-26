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
    public Guid SessionId { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeCodeAgentThread"/> class.
    /// </summary>
    /// <param name="sessionId">Optional session ID to resume a previous conversation.</param>
    internal ClaudeCodeAgentThread(Guid? sessionId = null)
    {
        SessionId = sessionId ?? Guid.NewGuid();
    }


    /// <inheritdoc/>
    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var state = new ThreadState
        {
            SessionId = SessionId.ToString(),
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
