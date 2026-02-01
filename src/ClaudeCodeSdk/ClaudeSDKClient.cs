using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace ClaudeCodeSdk;

/// <summary>
/// Client for interactive Claude Code sessions with manual connection lifecycle control.
/// Supports bidirectional communication, session management, and process interruption.
/// </summary>
public class ClaudeSdkClient : IAsyncDisposable
{
    private readonly ClaudeCodeOptions _options;
    private readonly ILogger? _logger;
    private ClaudeProcess? _process;
    private bool _disposed;

    /// <summary>
    /// Initialize Claude SDK client for interactive sessions.
    /// </summary>
    /// <param name="options">Optional configuration (defaults to ClaudeCodeOptions() if null)</param>
    /// <param name="logger">Optional logger for debugging</param>
    public ClaudeSdkClient(ClaudeCodeOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? new ClaudeCodeOptions();
        _logger = logger;
    }

    /// <summary>
    /// Connect to Claude with an optional initial prompt.
    /// </summary>
    /// <param name="prompt">Optional prompt string or async enumerable of messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ConnectAsync(object? prompt = null, CancellationToken cancellationToken = default)
    {
        if (_process != null)
            throw new CLIConnectionException("Already connected. Call DisconnectAsync() first.");

        _process = new ClaudeProcess(_options, null, _logger);
        await _process.StartAsync(prompt ?? CreateEmptyStream(), cancellationToken);
    }

    /// <summary>
    /// Receive all messages from Claude as a continuous stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of parsed messages</returns>
    public async IAsyncEnumerable<IMessage> ReceiveMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_process == null)
            throw new CLIConnectionException("Not connected. Call ConnectAsync() first.");

        await foreach (var data in _process.ReceiveAsync(cancellationToken))
        {
            yield return data;
        }
    }

    /// <summary>
    /// Send a new query in the current session.
    /// </summary>
    /// <param name="prompt">String message or async enumerable of message dictionaries</param>
    /// <param name="sessionId">Session identifier for the conversation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task QueryAsync(object prompt, string? sessionId = "default", CancellationToken cancellationToken = default)
    {
        if (_process == null)
            throw new CLIConnectionException("Not connected. Call ConnectAsync() first.");

        sessionId ??= "default";

        if (prompt is string stringPrompt)
        {
            var message = new Dictionary<string, object>
            {
                ["type"] = "user",
                ["message"] = new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["content"] = stringPrompt
                },
                ["parent_tool_use_id"] = null!,
                ["session_id"] = sessionId
            };

            await _process.SendAsync(new[] { message }, cancellationToken);
        }
        else if (prompt is IAsyncEnumerable<Dictionary<string, object>> asyncEnumerable)
        {
            async IAsyncEnumerable<Dictionary<string, object>> AddSessionIdAsync(
                IAsyncEnumerable<Dictionary<string, object>> source,
                [EnumeratorCancellation] CancellationToken token)
            {
                await foreach (var msg in source.WithCancellation(token))
                {
                    if (!msg.ContainsKey("session_id"))
                        msg["session_id"] = sessionId;
                    yield return msg;
                }
            }

            await _process.SendAsync(AddSessionIdAsync(asyncEnumerable, cancellationToken), cancellationToken);
        }
        else
        {
            throw new ArgumentException(
                "Prompt must be either a string or IAsyncEnumerable<Dictionary<string, object>>",
                nameof(prompt));
        }
    }

    /// <summary>
    /// Send interrupt signal to immediately kill the CLI process.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InterruptAsync(CancellationToken cancellationToken = default)
    {
        if (_process == null)
            throw new CLIConnectionException("Not connected. Call ConnectAsync() first.");

        await _process.InterruptAsync();
    }

    /// <summary>
    /// Receive messages until and including a ResultMessage, then automatically terminate.
    /// Convenience method for single-response workflows.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of messages ending with a ResultMessage</returns>
    public async IAsyncEnumerable<IMessage> ReceiveResponseAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReceiveMessagesAsync(cancellationToken))
        {
            yield return message;
            if (message is ResultMessage)
                yield break;
        }
    }

    /// <summary>
    /// Disconnect from Claude and cleanup process resources.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_process != null)
        {
            await _process.DisposeAsync();
            _process = null;
        }
    }

    private static async IAsyncEnumerable<Dictionary<string, object>> CreateEmptyStream()
    {
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// Dispose the client and disconnect if connected.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await DisconnectAsync();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
