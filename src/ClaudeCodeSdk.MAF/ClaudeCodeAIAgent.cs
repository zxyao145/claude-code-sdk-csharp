using ClaudeCodeSdk.MAF.Utils;
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
        ChatHistoryProvider = options?.ChatHistoryProvider;
    }

    public ChatHistoryProvider? ChatHistoryProvider { get; private set; }


    #region Serialize and Deserialize

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(AgentSession session, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session, nameof(session));

        if (session is not ClaudeCodeAgentSession typedSession)
        {
            throw new InvalidOperationException($"The provided session type '{session.GetType().Name}' is not compatible with this agent. Only sessions of type '{nameof(ChatClientAgentSession)}' can be serialized by this agent.");
        }

        var jso = jsonSerializerOptions ?? AgentSessionJsonUtil.ClaudeCodeAgentSession_OPTIONS;
        var jsonElement = JsonSerializer
            .SerializeToElement(typedSession, jso.GetTypeInfo(typeof(ClaudeCodeAgentSession)));

        return new(jsonElement);
    }


    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(JsonElement serializedState, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        if (serializedState.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The serialized session state must be a JSON object.", nameof(serializedState));
        }
        var jso = jsonSerializerOptions ?? AgentSessionJsonUtil.ClaudeCodeAgentSession_OPTIONS;


        var deserializeSession = serializedState
            .Deserialize(jso.GetTypeInfo(typeof(ClaudeCodeAgentSession)))
            as ClaudeCodeAgentSession;
        if (deserializeSession is null || deserializeSession.SessionId == Guid.Empty)
        {
            throw new ArgumentException("The serialized session state must contain a valid non-empty sessionId.", nameof(serializedState));
        }

        return new(deserializeSession);
    }

    #endregion

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
    {
        AgentSession session = NewSession();
        return ValueTask.FromResult(session);
    }

    private ClaudeCodeAgentSession NewSession()
    {
        return new ClaudeCodeAgentSession();
    }

    #region RunAsync

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default
        )
    {
        var claudeThread = session as ClaudeCodeAgentSession;

        (ClaudeCodeAgentSession safeSession,
            IEnumerable<ChatMessage> userAndChatHistoryMessages)
            = await PrepareSessionAndMessagesAsync(claudeThread, messages, cancellationToken);


        // Convert messages to Claude format and send (exclude System messages)
        var content = CombinedMessages(
                userAndChatHistoryMessages.Where(m => m.Role == ChatRole.User)
            );

        // Receive and collect all responses
        var responseMessages = new List<ChatMessage>();
        UsageDetails? usageDetails = null;
        if (!string.IsNullOrWhiteSpace(content))
        {
            var (asyncEnumMsgs, client) = await SendUserInput(null, content, cancellationToken);

            if (client != null && cancellationToken.IsCancellationRequested)
            {
                await InterruptAsync(client);
                cancellationToken.ThrowIfCancellationRequested();
            }

            await foreach (var claudeMessage in asyncEnumMsgs)
            {
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

        await SaveNewMessagesAsync(safeSession, userAndChatHistoryMessages, responseMessages, cancellationToken);

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
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var claudeThread = session as ClaudeCodeAgentSession;

        (ClaudeCodeAgentSession safeSession,
           IEnumerable<ChatMessage> userAndChatHistoryMessages)
           = await PrepareSessionAndMessagesAsync(claudeThread, messages, cancellationToken);

        var content = CombinedMessages(
                messages.Where(m => m.Role == ChatRole.User)
            );

        if (!string.IsNullOrWhiteSpace(content))
        {
            var (asyncEnumMsgs, client) = await SendUserInput(claudeThread, content, cancellationToken);

            if (client != null && cancellationToken.IsCancellationRequested)
            {
                await InterruptAsync(client);
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (ChatHistoryProvider is null)
            {
                await foreach (var claudeMessage in asyncEnumMsgs)
                {
                    var update = claudeMessage.ToAgentRunResponseUpdate();
                    if (update != null)
                    {
                        yield return update;
                    }
                }
            }
            else
            {
                List<ChatMessage> responseMessages = new();
                // Receive and yield responses
                await foreach (var claudeMessage in asyncEnumMsgs)
                {
                    var update = claudeMessage.ToAgentRunResponseUpdate();
                    if (update != null)
                    {
                        var chatMessage = update.ToChatMessage();
                        responseMessages.Add(chatMessage);
                        yield return update;
                    }
                }

                await SaveNewMessagesAsync(safeSession, userAndChatHistoryMessages, responseMessages, cancellationToken);
            }
        }
    }


    #endregion


    #region ChatHistoryProvider

    private async ValueTask<(ClaudeCodeAgentSession AgentSession, IEnumerable<ChatMessage> HistoryMessages)> PrepareSessionAndMessagesAsync(
        AgentSession? session,
        IEnumerable<ChatMessage> inputMessages,
        CancellationToken cancellationToken)
    {
        IEnumerable<ChatMessage> userAndChatHistoryMessages = inputMessages;
        if (ChatHistoryProvider is not null)
        {
#pragma warning disable MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var invokingContext = new ChatHistoryProvider.InvokingContext(this, session, inputMessages);
#pragma warning restore MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            userAndChatHistoryMessages = await this.ChatHistoryProvider.InvokingAsync(invokingContext, cancellationToken).ConfigureAwait(false);
        }
        session ??= await this.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        if (session is not ClaudeCodeAgentSession typedSession)
        {
            throw new InvalidOperationException($"The provided session type '{session.GetType().Name}' is not compatible with this agent. Only sessions of type '{nameof(ChatClientAgentSession)}' can be used by this agent.");
        }
        return (typedSession, userAndChatHistoryMessages);
    }

    private async ValueTask SaveNewMessagesAsync(
        ClaudeCodeAgentSession session,
        IEnumerable<ChatMessage> requestMessages,
        IEnumerable<ChatMessage> responseMessages,
        CancellationToken cancellationToken)
    {
        if (ChatHistoryProvider is not null)
        {
#pragma warning disable MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var invokedContext = new ChatHistoryProvider.InvokedContext(this, session, requestMessages, responseMessages);
#pragma warning restore MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            await ChatHistoryProvider.InvokedAsync(invokedContext, cancellationToken);
        }
    }

    #endregion


    private async Task InterruptAsync(ClaudeSdkClient client)
    {
        await client.InterruptAsync(CancellationToken.None);
    }

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
            client = await _clientManager.GetClientAsync(claudeThread, CancellationToken.None);

            await client.QueryAsync(content,
                 sessionId: claudeThread.SessionId.ToString(),
                 cancellationToken: CancellationToken.None);

            asyncEnumMsgs = client.ReceiveResponseAsync(CancellationToken.None);
        }

        // 
        var interruptRequested = 0;
        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            if (client == null) return;
            if (Interlocked.Exchange(ref interruptRequested, 1) != 0)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await InterruptAsync(client);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to interrupt Claude SDK client during streaming cancellation");
                }
            });
        });


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
