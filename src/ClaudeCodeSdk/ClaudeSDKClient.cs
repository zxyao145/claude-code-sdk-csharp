using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Exceptions;
using ClaudeCodeSdk.Internal.Transport;
using ClaudeCodeSdk.Internal;

namespace ClaudeCodeSdk;

public class ClaudeSDKClient : IAsyncDisposable
{
    private readonly ClaudeCodeOptions _options;
    private readonly ILogger? _logger;
    private ITransport? _transport;
    private bool _disposed;

    /// <summary>
    /// Initialize Claude SDK client.
    /// </summary>
    /// <param name="options">Optional configuration (defaults to ClaudeCodeOptions() if null)</param>
    /// <param name="logger">Optional logger for debugging</param>
    public ClaudeSDKClient(ClaudeCodeOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? new ClaudeCodeOptions();
        _logger = logger;
        Environment.SetEnvironmentVariable("CLAUDE_CODE_ENTRYPOINT", "sdk-csharp-client");
    }

    /// <summary>
    /// Connect to Claude with a prompt or message stream.
    /// </summary>
    /// <param name="prompt">Optional prompt string or async enumerable of messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ConnectAsync(object? prompt = null, Dictionary<string, string?>? environmentVariables = null, CancellationToken cancellationToken = default)
    {
        if (_transport != null)
            throw new CLIConnectionException("Already connected. Call DisconnectAsync() first.");

        // Auto-connect with empty async iterable if no prompt is provided
        object actualPrompt = prompt ?? CreateEmptyStream();

        _transport = new SubprocessCliTransport(actualPrompt, _options, null, _logger);
        await _transport.ConnectAsync(cancellationToken);
    }

    /// <summary>
    /// Receive all messages from Claude.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of messages</returns>
    public async IAsyncEnumerable<IMessage> ReceiveMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_transport == null)
            throw new CLIConnectionException("Not connected. Call ConnectAsync() first.");

        await foreach (var data in _transport.ReceiveMessagesAsync(cancellationToken))
        {
            yield return MessageParser.ParseMessage(data, _logger);
        }
    }

    /// <summary>
    /// Send a new request in streaming mode.
    /// </summary>
    /// <param name="prompt">String message or async enumerable of message dictionaries</param>
    /// <param name="sessionId">Session identifier for the conversation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task QueryAsync(object prompt, string sessionId = "default", CancellationToken cancellationToken = default)
    {
        if (_transport == null)
            throw new CLIConnectionException("Not connected. Call ConnectAsync() first.");

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

            await _transport.SendRequestAsync(new[] { message }, new Dictionary<string, object> { ["session_id"] = sessionId }, cancellationToken);
        }
        else if (prompt is IAsyncEnumerable<Dictionary<string, object>> asyncEnumerable)
        {
            var messages = new List<Dictionary<string, object>>();
            await foreach (var msg in asyncEnumerable.WithCancellation(cancellationToken))
            {
                if (!msg.ContainsKey("session_id"))
                {
                    msg["session_id"] = sessionId;
                }
                messages.Add(msg);
            }

            if (messages.Count > 0)
            {
                await _transport.SendRequestAsync(messages, new Dictionary<string, object> { ["session_id"] = sessionId }, cancellationToken);
            }
        }
        else
        {
            throw new ArgumentException("Prompt must be either a string or IAsyncEnumerable<Dictionary<string, object>>", nameof(prompt));
        }
    }

    /// <summary>
    /// Send interrupt signal (only works with streaming mode).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task InterruptAsync(CancellationToken cancellationToken = default)
    {
        if (_transport == null)
            throw new CLIConnectionException("Not connected. Call ConnectAsync() first.");
        
        await _transport.InterruptAsync(cancellationToken);
    }

    /// <summary>
    /// Receive messages from Claude until and including a ResultMessage.
    /// 
    /// This async iterator yields all messages in sequence and automatically terminates
    /// after yielding a ResultMessage (which indicates the response is complete).
    /// It's a convenience method over ReceiveMessagesAsync() for single-response workflows.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of messages ending with a ResultMessage</returns>
    public async IAsyncEnumerable<IMessage> ReceiveResponseAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReceiveMessagesAsync(cancellationToken))
        {
            yield return message;
            if (message is ResultMessage)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Disconnect from Claude.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_transport != null)
        {
            await _transport.DisconnectAsync(cancellationToken);
            await _transport.DisposeAsync();
            _transport = null;
        }
    }

    private static async IAsyncEnumerable<Dictionary<string, object>> CreateEmptyStream()
    {
        // This creates an empty async enumerable that never yields but keeps the connection open
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