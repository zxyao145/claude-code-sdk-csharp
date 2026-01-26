using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ClaudeCodeSdk.MAF;

/// <summary>
/// AIAgent implementation using ClaudeCodeSdk for Claude Code interactions.
/// Implements IDisposable and IAsyncDisposable for proper resource management of the underlying ClaudeSdkClient.
/// </summary>
public class ClaudeCodeAIAgent : AIAgent, IDisposable, IAsyncDisposable
{
    private readonly ClaudeCodeAIAgentOptions _options;
    private readonly ILogger? _logger;
    private ClaudeSdkClient? _client;
    private bool _isConnected;
    private bool _disposed;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    /// <summary>
    /// ClaudeCodeOptions.Resume will not working. Please replace with AgentThread
    /// 
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public ClaudeCodeAIAgent(ClaudeCodeOptions? options = null, ILogger? logger = null)
        : this(ClaudeCodeAIAgentOptions.From(options), logger)
    {

    }

    public ClaudeCodeAIAgent(ClaudeCodeAIAgentOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? new ClaudeCodeAIAgentOptions();
        _logger = logger;
    }

    public override AgentThread DeserializeThread(JsonElement serializedThread, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var sessionId = serializedThread.TryGetProperty("sessionId", out var sidProp)
            ? sidProp.GetString() : null;

        return new ClaudeCodeAgentThread(sessionId);
    }

    public override AgentThread GetNewThread()
    {
        return NewThread();
    }

    private ClaudeCodeAgentThread NewThread()
    {
        return new ClaudeCodeAgentThread();
    }

    /// <summary>
    /// Ensures the client is connected, creating and connecting lazily on first use.
    /// Thread-safe with double-check locking.
    /// </summary>
    private async ValueTask EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isConnected) return;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_isConnected) return;

            _client ??= new ClaudeSdkClient(_options.ToClaudeCodeOptions(), _logger);
            await _client.ConnectAsync(cancellationToken: cancellationToken);
            _isConnected = true;
        }
        finally
        {
            _connectionLock.Release();
        }
    }


    #region RunAsync

    public override async Task<AgentRunResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default
        )
    {
        var claudeThread = (thread as ClaudeCodeAgentThread ?? NewThread())!;
        var messagesList = messages.ToList();

        // Ensure connection is established (lazy initialization)
        await EnsureConnectedAsync(cancellationToken);

        // Convert messages to Claude format and send (exclude System messages)
        var combinedMessages = messagesList
            .Where(m => m.Role == ChatRole.User)
            .ToList();

        // Receive and collect all responses
        var responseMessages = new List<ChatMessage>();
        UsageDetails? usageDetails = null;
        foreach (var message in combinedMessages)
        {
            if (message.Role == ChatRole.User)
            {
                var content = message.Text ?? string.Empty;
                await _client!.QueryAsync(content,
                         sessionId: claudeThread.SessionId,
                         cancellationToken: cancellationToken);

                await foreach (var claudeMessage in _client.ReceiveResponseAsync(cancellationToken))
                {
                    claudeThread.SetSessionIdIfNull(claudeMessage);

                    if (claudeMessage is ResultMessage resultMessage)
                    {
                        usageDetails = resultMessage.ToUsageDetails();
                    }
                    else
                    {
                        var assistantMessage = claudeMessage.ToChatMessage();
                        if (assistantMessage != null)
                        {
                            responseMessages.Add(assistantMessage);
                        }
                    }
                }
            }
        }

        // Return complete response
        return new AgentRunResponse
        {
            Usage = usageDetails,
            Messages = responseMessages,
            ResponseId = Guid.NewGuid().ToString()
        };
    }

    #endregion


    #region RunStreamingAsync

    public override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var claudeThread = (thread as ClaudeCodeAgentThread ?? NewThread())!;
        var messagesList = messages.ToList();

        // Ensure connection is established (lazy initialization)
        await EnsureConnectedAsync(cancellationToken);

        // Convert messages to Claude format and send (exclude System messages)
        var combinedMessages = messagesList
            .Where(m => m.Role == ChatRole.User)
            .ToList();

        foreach (var message in combinedMessages)
        {
            var content = message.Text ?? string.Empty;
            await _client!.QueryAsync(content,
                     sessionId: claudeThread.SessionId,
                     cancellationToken: cancellationToken);

            // Receive and yield responses
            await foreach (var claudeMessage in _client.ReceiveResponseAsync(cancellationToken))
            {
                claudeThread.SetSessionIdIfNull(claudeMessage);

                var update = claudeMessage.ToAgentRunResponseUpdate();
                if (update != null)
                {
                    yield return update;
                }
            }
        }
    }


    #endregion


    #region IDisposable / IAsyncDisposable

    /// <summary>
    /// Disposes the agent and releases the underlying ClaudeSdkClient resources.
    /// Prefer using DisposeAsync when possible for proper async cleanup.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        if (_client != null)
        {
            _client.DisconnectAsync().GetAwaiter().GetResult();
            _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _client = null;
        }

        _connectionLock.Dispose();
        _isConnected = false;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disposes the agent and releases the underlying ClaudeSdkClient resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        if (_client != null)
        {
            await _client.DisconnectAsync();
            await _client.DisposeAsync();
            _client = null;
        }

        _connectionLock.Dispose();
        _isConnected = false;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}