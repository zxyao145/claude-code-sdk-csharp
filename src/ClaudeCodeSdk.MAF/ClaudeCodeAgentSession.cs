using Microsoft.Agents.AI;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.MAF;

/// <summary>
/// Provides a AgentSession implementation for use with <see cref="ClaudeCodeAIAgent"/>.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class ClaudeCodeAgentSession : AgentSession
{
    /// <summary>
    /// Gets the session ID for the Claude Code conversation.
    /// </summary>
    /// <remarks>
    /// This property is generated when a new session is created or restored from serialized state.
    /// It is sent to Claude Code as the user message's session ID.
    /// </remarks>
    [JsonPropertyName("sessionId")]
    public Guid SessionId { get; internal set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeCodeAgentSession"/> class.
    /// </summary>
    /// <param name="sessionId">Optional session ID to resume a previous conversation.</param>
    internal ClaudeCodeAgentSession(Guid? sessionId = null)
    {
        SessionId = sessionId ?? Guid.NewGuid();
    }

    [JsonConstructor]
    internal ClaudeCodeAgentSession(Guid sessionId, AgentSessionStateBag? stateBag)
        : base(stateBag ?? new())
    {
        this.SessionId = sessionId;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay =>
        SessionId is { } sessionId
            ? $"SessionId = {sessionId}, StateBag Count = {this.StateBag.Count}"
            : $"StateBag Count = {this.StateBag.Count}";
}
