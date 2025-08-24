using ClaudeCodeSdk.Internal;
using ClaudeCodeSdk.Internal.Transport;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace ClaudeCodeSdk;

/// <summary>
/// Static class providing query functionality for one-shot interactions with Claude Code.
/// </summary>
public static class ClaudeQuery
{
    /// <summary>
    /// Query Claude Code for one-shot or unidirectional streaming interactions.
    /// 
    /// This function is ideal for simple, stateless queries where you don't need
    /// bidirectional communication or conversation management. For interactive,
    /// stateful conversations, use ClaudeSDKClient instead.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude. Can be a string for single-shot queries
    /// or an IAsyncEnumerable for streaming mode with continuous interaction.</param>
    /// <param name="options">Optional configuration (defaults to ClaudeCodeOptions() if null)</param>
    /// <param name="transport">Optional transport implementation</param>
    /// <param name="logger">Optional logger for debugging</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of messages from the conversation</returns>
    public static async IAsyncEnumerable<IMessage> QueryAsync(
        object prompt,
        ClaudeCodeOptions? options = null,
        ITransport? transport = null,
        ILogger? logger = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ClaudeCodeOptions();
        Environment.SetEnvironmentVariable("CLAUDE_CODE_ENTRYPOINT", "sdk-csharp");

        var client = new InternalClient(logger);
        await foreach (var message in client.ProcessQueryAsync(prompt, options, transport, cancellationToken))
        {
            yield return message;
        }
    }
}
