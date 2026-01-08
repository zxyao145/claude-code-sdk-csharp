using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace ClaudeCodeSdk;

/// <summary>
/// Static class providing query functionality for one-shot interactions with Claude Code.
/// Fire-and-forget pattern with automatic connection lifecycle management.
/// </summary>
public static class ClaudeQuery
{
    /// <summary>
    /// Query Claude Code for one-shot interactions with automatic cleanup.
    ///
    /// This method is ideal for simple, stateless queries where you don't need
    /// bidirectional communication or conversation management.
    /// For interactive sessions, use ClaudeSdkClient instead.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude (string or IAsyncEnumerable)</param>
    /// <param name="options">Optional configuration (defaults to ClaudeCodeOptions() if null)</param>
    /// <param name="logger">Optional logger for debugging</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of messages from the conversation</returns>
    public static async IAsyncEnumerable<IMessage> QueryAsync(
        object prompt,
        ClaudeCodeOptions? options = null,
        ILogger? logger = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ClaudeCodeOptions();

        var process = new ClaudeProcess(options, null, logger);
        await using (process)
        {
            await process.StartAsync(prompt, cancellationToken);

            await foreach (var data in process.ReceiveAsync(cancellationToken))
            {
                yield return MessageParser.ParseMessage(data, logger);
            }
        }
    }
}
