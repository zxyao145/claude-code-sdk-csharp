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
    private readonly ClaudeSdkClientManager _clientManager;
    private bool _disposed;

    public ClaudeCodeAIAgent() : this(new ClaudeCodeAIAgentOptions(), null)
    {

    }

    /// <summary>
    /// ClaudeCodeOptions.Resume will not working. Please replace with AgentSession
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
        _clientManager = new ClaudeSdkClientManager(_options.ToClaudeCodeOptions(), _logger);
    }

    public override ValueTask<AgentSession> DeserializeSessionAsync(JsonElement serializedThread, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        var sessionId = serializedThread.TryGetProperty("sessionId", out var sidProp)
            ? sidProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new Exception("ClaudeCodeAIAgent cannot get sessionId in DeserializeThread");
        }
        Guid guid = Guid.Parse(sessionId);
        AgentSession thread = new ClaudeCodeAgentSession(guid);
        return ValueTask.FromResult(thread);
    }


    public override ValueTask<AgentSession> GetNewSessionAsync(CancellationToken cancellationToken = default)
    {
        AgentSession thread = NewThread();
        return ValueTask.FromResult(thread);
    }

    private ClaudeCodeAgentSession NewThread()
    {
        return new ClaudeCodeAgentSession();
    }

    #region RunAsync

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default
        )
    {
        var claudeThread = thread as ClaudeCodeAgentSession;
        var messagesList = messages.ToList();

        // Convert messages to Claude format and send (exclude System messages)
        var content = CombinedMessages(
                messagesList.Where(m => m.Role == ChatRole.User)
            );

        // Receive and collect all responses
        var responseMessages = new List<ChatMessage>();
        UsageDetails? usageDetails = null;
        if (!string.IsNullOrWhiteSpace(content))
        {
            var (asyncEnumMsgs, client) = await SendUserInput(claudeThread, content, cancellationToken);

            if (client != null && cancellationToken.IsCancellationRequested)
            {
                await client.InterruptAsync(CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();
            }

            await foreach (var claudeMessage in asyncEnumMsgs)
            {
                if (client != null && cancellationToken.IsCancellationRequested)
                {
                    await client.InterruptAsync(CancellationToken.None);
                    cancellationToken.ThrowIfCancellationRequested();
                }

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

        // Return complete response
        return new AgentResponse
        {
            Usage = usageDetails,
            Messages = responseMessages,
            ResponseId = Guid.NewGuid().ToString()
        };
    }


    #endregion


    #region RunStreamingAsync

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? thread = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var claudeThread = thread as ClaudeCodeAgentSession;
        var messagesList = messages.ToList();
        var content = CombinedMessages(
                messagesList.Where(m => m.Role == ChatRole.User)
            );

        if (!string.IsNullOrWhiteSpace(content))
        {
            var (asyncEnumMsgs, client) = await SendUserInput(claudeThread, content, cancellationToken);

            if (client != null && cancellationToken.IsCancellationRequested)
            {
                await client.InterruptAsync(CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Receive and yield responses
            await foreach (var claudeMessage in asyncEnumMsgs)
            {
                if (client != null && cancellationToken.IsCancellationRequested)
                {
                    await client.InterruptAsync(CancellationToken.None);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var update = claudeMessage.ToAgentRunResponseUpdate();
                if (update != null)
                {
                    yield return update;
                }
            }
        }
    }


    #endregion


    private string? CombinedMessages(IEnumerable<ChatMessage> userMessages)
    {
        // Convert messages to Claude format and send (exclude System messages)
        return userMessages.FirstOrDefault()?.Text ?? "";
    }

    private async Task<(IAsyncEnumerable<IMessage> Messages, ClaudeSdkClient? Client)> SendUserInput(
        ClaudeCodeAgentSession? claudeThread,
        string content,
        CancellationToken cancellationToken)
    {
        IAsyncEnumerable<IMessage> asyncEnumMsgs;
        ClaudeSdkClient? client = null;
        if (claudeThread == null)
        {
            asyncEnumMsgs = ClaudeQuery.QueryAsync(content, options: _options.ToClaudeCodeOptions(), _logger);
        }
        else
        {
            client = await _clientManager.GetClientAsync(claudeThread, cancellationToken);

            await client.QueryAsync(content,
                 sessionId: claudeThread.ConversationId.ToString(),
                 cancellationToken: cancellationToken);

            asyncEnumMsgs = client.ReceiveResponseAsync(cancellationToken);
        }

        return (asyncEnumMsgs, client);
    }


    #region IDisposable / IAsyncDisposable

    /// <summary>
    /// Disposes the agent and releases the underlying ClaudeSdkClient resources.
    /// Prefer using DisposeAsync when possible for proper async cleanup.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _clientManager.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disposes the agent and releases the underlying ClaudeSdkClient resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _clientManager.DisposeAsync();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
