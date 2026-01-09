global using ClaudeCodeSdk.Exceptions;
global using ClaudeCodeSdk.Types;

namespace ClaudeCodeSdk;

/// <summary>
/// Claude Code SDK for .NET - Main entry point for Claude Code integration.
/// 
/// This SDK provides two main ways to interact with Claude Code:
/// 1. ClaudeQuery.QueryAsync() - For simple, one-shot queries
/// 2. ClaudeSDKClient - For interactive, bidirectional conversations
/// </summary>
public static class ClaudeCodeSDK
{
    /// <summary>
    /// Current version of the Claude Code SDK.
    /// </summary>
    public const string Version = "0.0.20";

    /// <summary>
    /// Convenience method for simple queries. Equivalent to ClaudeQuery.QueryAsync().
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude</param>
    /// <param name="options">Optional configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of messages</returns>
    public static IAsyncEnumerable<IMessage> QueryAsync(
        string prompt,
        ClaudeCodeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return ClaudeQuery.QueryAsync(prompt, options, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Create a new Claude SDK client for interactive conversations.
    /// </summary>
    /// <param name="options">Optional configuration</param>
    /// <returns>New ClaudeSDKClient instance</returns>
    public static ClaudeSdkClient CreateClient(ClaudeCodeOptions? options = null)
    {
        return new ClaudeSdkClient(options);
    }
}