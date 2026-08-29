using System.Runtime.CompilerServices;
using System.Text.Json;
using ClaudeCodeSdk.MAF.Utils;
using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

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

    public ClaudeCodeAIAgent()
        : this(new ClaudeCodeAIAgentOptions(), null) { }

    /// <summary>
    /// ClaudeCodeOptions.Resume will not working. Please replace with AgentSession
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public ClaudeCodeAIAgent(ClaudeCodeOptions? options = null, ILogger? logger = null)
        : this(ClaudeCodeAIAgentOptions.From(options), logger) { }

    public ClaudeCodeAIAgent(ClaudeCodeAIAgentOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? new ClaudeCodeAIAgentOptions();
        _logger = logger;
        _clientManager = new ClaudeSdkClientManager(_options.ToClaudeCodeOptions(), _logger);
        ChatHistoryProvider = options?.ChatHistoryProvider;
    }

    public ChatHistoryProvider? ChatHistoryProvider { get; private set; }

    public override string Name => "ClaudeCode";

    #region Serialize and Deserialize

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(session, nameof(session));

        if (session is not ClaudeCodeAgentSession typedSession)
        {
            throw new InvalidOperationException(
                $"The provided session type '{session.GetType().Name}' is not compatible with this agent. Only sessions of type '{nameof(ChatClientAgentSession)}' can be serialized by this agent."
            );
        }

        var jso = jsonSerializerOptions ?? AgentSessionJsonUtil.ClaudeCodeAgentSession_OPTIONS;
        var jsonElement = JsonSerializer.SerializeToElement(
            typedSession,
            jso.GetTypeInfo(typeof(ClaudeCodeAgentSession))
        );

        return new(jsonElement);
    }

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        if (serializedState.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "The serialized session state must be a JSON object.",
                nameof(serializedState)
            );
        }
        var jso = jsonSerializerOptions ?? AgentSessionJsonUtil.ClaudeCodeAgentSession_OPTIONS;

        var deserializeSession =
            serializedState.Deserialize(jso.GetTypeInfo(typeof(ClaudeCodeAgentSession)))
            as ClaudeCodeAgentSession;
        if (deserializeSession is null || deserializeSession.SessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "The serialized session state must contain a valid non-empty sessionId.",
                nameof(serializedState)
            );
        }

        return new(deserializeSession);
    }

    #endregion

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(
        CancellationToken cancellationToken = default
    )
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
        var requestMessages = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var safeSession = await PrepareSessionAsync(
            claudeThread,
            requestMessages,
            cancellationToken
        );

        var content = ClaudeMafPromptBuilder.Create(requestMessages, "default");
        IAsyncEnumerable<IMessage> responseStream;
        ClaudeSdkClient? client = null;
        if (content is not null)
        {
            (responseStream, client) = await SendUserInput(null, content, cancellationToken);
        }
        else
        {
            responseStream = EmptyMessagesAsync();
        }

        using var cancellationRegistration = RegisterInterrupt(client, cancellationToken);
        return await ProcessNonStreamingMessagesAsync(
                responseStream,
                safeSession,
                requestMessages,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    internal async Task<AgentResponse> ProcessNonStreamingMessagesAsync(
        IAsyncEnumerable<IMessage> messages,
        ClaudeCodeAgentSession session,
        IReadOnlyList<ChatMessage> requestMessages,
        CancellationToken cancellationToken = default
    )
    {
        var responseMessages = new List<ChatMessage>();
        UsageDetails? usageDetails = null;
        await foreach (
            var processedMessage in ProcessMessagesWithHistoryAsync(
                messages,
                session,
                requestMessages,
                includeMappedUpdates: false,
                cancellationToken: cancellationToken
            )
        )
        {
            if (processedMessage.Message is ResultMessage resultMessage)
            {
                usageDetails = resultMessage.ToUsageDetails();
            }

            if (processedMessage.Message.ToChatMessage() is { } responseMessage)
            {
                responseMessages.Add(responseMessage);
            }
        }

        return new AgentResponse
        {
            Usage = usageDetails,
            Messages = responseMessages,
            ResponseId = Guid.NewGuid().ToString(),
        };
    }

    #endregion

    #region RunStreamingAsync

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var claudeThread = session as ClaudeCodeAgentSession;
        var requestMessages = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var safeSession = await PrepareSessionAsync(
            claudeThread,
            requestMessages,
            cancellationToken
        );

        var content = ClaudeMafPromptBuilder.Create(
            requestMessages,
            claudeThread?.SessionId.ToString() ?? "default"
        );

        if (content is not null)
        {
            var (asyncEnumMsgs, client) = await SendUserInput(
                claudeThread,
                content,
                cancellationToken
            );
            using var cancellationRegistration = RegisterInterrupt(client, cancellationToken);
            await foreach (
                var update in ProcessStreamingMessagesAsync(
                    asyncEnumMsgs,
                    safeSession,
                    requestMessages,
                    cancellationToken
                )
            )
            {
                yield return update;
            }
        }
    }

    internal async IAsyncEnumerable<AgentResponseUpdate> ProcessStreamingMessagesAsync(
        IAsyncEnumerable<IMessage> messages,
        ClaudeCodeAgentSession session,
        IReadOnlyList<ChatMessage> requestMessages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (
            var processedMessage in ProcessMessagesWithHistoryAsync(
                messages,
                session,
                requestMessages,
                includeMappedUpdates: true,
                cancellationToken: cancellationToken
            )
        )
        {
            foreach (var update in processedMessage.Updates)
            {
                yield return update;
            }
        }
    }

    private async IAsyncEnumerable<MessageWithUpdates> ProcessMessagesWithHistoryAsync(
        IAsyncEnumerable<IMessage> messages,
        ClaudeCodeAgentSession session,
        IReadOnlyList<ChatMessage> requestMessages,
        bool includeMappedUpdates,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var enableHistoryPersistence = ChatHistoryProvider != null;
        var processor = new ClaudeStreamingMessageProcessor(
            requestMessages,
            enableHistoryPersistence,
            enableMessageMapping: includeMappedUpdates
        );
        Exception? processingFailure = null;
        await using var enumerator = messages
            .WithCancellation(cancellationToken)
            .GetAsyncEnumerator();
        try
        {
            while (true)
            {
                MessageWithUpdates processedMessage;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }

                    var message = enumerator.Current;
                    var mappedMessage = processor.Process(message);
                    if (mappedMessage.CompletedHistoryBatch is { } completedBatch)
                    {
                        await PersistStreamingBatchAsync(session, completedBatch, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    processedMessage = new MessageWithUpdates(
                        message,
                        includeMappedUpdates ? mappedMessage.Updates : []
                    );
                }
                catch (Exception exception)
                {
                    processingFailure = exception;
                    throw;
                }

                yield return processedMessage;
            }
        }
        finally
        {
            try
            {
                if (processor.CompleteRun() is { } finalBatch)
                {
                    await PersistStreamingBatchAsync(session, finalBatch, CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception persistenceException) when (processingFailure != null)
            {
                _logger?.LogError(
                    persistenceException,
                    "Claude Code history persistence failed while preserving a message stream failure."
                );
            }
        }
    }

    private ValueTask PersistStreamingBatchAsync(
        ClaudeCodeAgentSession session,
        ClaudeHistoryBatch batch,
        CancellationToken cancellationToken
    )
    {
        IEnumerable<ChatMessage> responseMessages =
            batch.ResponseUpdates.Count == 0
                ? []
                : batch.ResponseUpdates.ToAgentResponse().Messages;
        return SaveNewMessagesAsync(
            session,
            batch.RequestMessages,
            responseMessages,
            cancellationToken
        );
    }

    #endregion


    #region ChatHistoryProvider

    private async ValueTask<ClaudeCodeAgentSession> PrepareSessionAsync(
        AgentSession? session,
        IEnumerable<ChatMessage> inputMessages,
        CancellationToken cancellationToken
    )
    {
        if (ChatHistoryProvider is not null)
        {
#pragma warning disable MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var invokingContext = new ChatHistoryProvider.InvokingContext(
                this,
                session,
                inputMessages
            );
#pragma warning restore MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            // Claude Code resumes provider-managed conversations by session ID, so stored history is not resent as prompt input.
            _ = await this
                .ChatHistoryProvider.InvokingAsync(invokingContext, cancellationToken)
                .ConfigureAwait(false);
        }
        session ??= await this.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        if (session is not ClaudeCodeAgentSession typedSession)
        {
            throw new InvalidOperationException(
                $"The provided session type '{session.GetType().Name}' is not compatible with this agent. Only sessions of type '{nameof(ChatClientAgentSession)}' can be used by this agent."
            );
        }
        return typedSession;
    }

    private async ValueTask SaveNewMessagesAsync(
        ClaudeCodeAgentSession session,
        IEnumerable<ChatMessage> requestMessages,
        IEnumerable<ChatMessage> responseMessages,
        CancellationToken cancellationToken
    )
    {
        if (ChatHistoryProvider is not null)
        {
#pragma warning disable MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var invokedContext = new ChatHistoryProvider.InvokedContext(
                this,
                session,
                requestMessages,
                responseMessages
            );
#pragma warning restore MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            await ChatHistoryProvider.InvokedAsync(invokedContext, cancellationToken);
        }
    }

    #endregion


    private async Task InterruptAsync(ClaudeSdkClient client)
    {
        await client.InterruptAsync(CancellationToken.None);
    }

    private static async IAsyncEnumerable<IMessage> EmptyMessagesAsync()
    {
        yield break;
    }

    private async Task<(
        IAsyncEnumerable<IMessage> Messages,
        ClaudeSdkClient? Client
    )> SendUserInput(
        ClaudeCodeAgentSession? claudeThread,
        object content,
        CancellationToken cancellationToken
    )
    {
        IAsyncEnumerable<IMessage> asyncEnumMsgs;
        ClaudeSdkClient? client = null;
        if (claudeThread == null)
        {
            asyncEnumMsgs = ClaudeQuery.QueryAsync(
                content,
                options: _options.ToClaudeCodeOptions(),
                _logger,
                cancellationToken
            );
        }
        else
        {
            client = await _clientManager.GetClientAsync(claudeThread, cancellationToken);

            await client.QueryAsync(
                content,
                sessionId: claudeThread.SessionId.ToString(),
                cancellationToken: cancellationToken
            );

            asyncEnumMsgs = client.ReceiveResponseAsync(cancellationToken);
        }
        return (asyncEnumMsgs, client);
    }

    private CancellationTokenRegistration RegisterInterrupt(
        ClaudeSdkClient? client,
        CancellationToken cancellationToken
    )
    {
        if (client == null)
        {
            return default;
        }

        var interruptRequested = 0;
        return cancellationToken.Register(() =>
        {
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
                    _logger?.LogDebug(
                        ex,
                        "Failed to interrupt Claude SDK client during cancellation"
                    );
                }
            });
        });
    }

    private readonly record struct MessageWithUpdates(
        IMessage Message,
        IReadOnlyList<AgentResponseUpdate> Updates
    );

    #region IDisposable / IAsyncDisposable

    /// <summary>
    /// Disposes the agent and releases the underlying ClaudeSdkClient resources.
    /// Prefer using DisposeAsync when possible for proper async cleanup.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _clientManager.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disposes the agent and releases the underlying ClaudeSdkClient resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await _clientManager.DisposeAsync();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
