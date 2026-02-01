using ClaudeCodeSdk.Types;
using Microsoft.Agents.AI;
using System.Diagnostics;
using System.Text.Json;

namespace ClaudeCodeSdk.MAF;

/// <summary>
/// Provides a thread implementation for use with <see cref="ClaudeCodeAIAgent"/>.
/// Copy from Microsoft.Agents.AI.ChatClientAgentSession.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class ClaudeCodeAgentSession : AgentSession
{
    private ChatHistoryProvider? _chatHistoryProvider;


    /// <summary>
    /// Gets the session ID for the Claude Code conversation.
    /// </summary>
    /// <remarks>
    /// This property is set automatically when receiving the first <see cref="SystemMessage"/>
    /// from Claude Code that contains a session ID.
    /// </remarks>
    /// /// ConversationId
    public Guid? ConversationId
    {
        get;
        internal set
        {
            if (field == null && value == null)
            {
                return;
            }

            if (this._chatHistoryProvider is not null)
            {
                // If we have a ChatHistoryProvider already, we shouldn't switch the session to use a conversation id
                // since it means that the session contents will essentially be deleted, and the session will not work
                // with the original agent anymore.
                throw new InvalidOperationException("Only the ConversationId or ChatHistoryProvider may be set, but not both and switching from one to another is not supported.");
            }
            if (value == null)
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));
            }

            field = value;
        }
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeCodeAgentSession"/> class.
    /// </summary>
    /// <param name="sessionId">Optional session ID to resume a previous conversation.</param>
    internal ClaudeCodeAgentSession(Guid? sessionId = null)
    {
        ConversationId = sessionId ?? Guid.NewGuid();
    }

    //internal ClaudeCodeAgentSession()
    //{
    //}

    public ChatHistoryProvider? ChatHistoryProvider
    {
        get => this._chatHistoryProvider;
        internal set
        {
            if (this._chatHistoryProvider is null && value is null)
            {
                return;
            }

            if (this.ConversationId != null)
            {
                // If we have a conversation id already, we shouldn't switch the session to use a ChatHistoryProvider
                // since it means that the session will not work with the original agent anymore.
                throw new InvalidOperationException("Only the ConversationId or ChatHistoryProvider may be set, but not both and switching from one to another is not supported.");
            }
            if (value == null)
            {
                ArgumentNullException.ThrowIfNull(value, nameof(value));
            }
            this._chatHistoryProvider = value;
        }
    }


    /// <summary>
    /// Gets or sets the <see cref="AIContextProvider"/> used by this thread to provide additional context to the AI model before each invocation.
    /// </summary>
    public AIContextProvider? AIContextProvider { get; internal set; }


    /// <summary>
    /// Creates a new instance of the <see cref="ChatClientAgentSession"/> class from previously serialized state.
    /// </summary>
    /// <param name="serializedSessionState">A <see cref="JsonElement"/> representing the serialized state of the session.</param>
    /// <param name="jsonSerializerOptions">Optional settings for customizing the JSON deserialization process.</param>
    /// <param name="chatHistoryProviderFactory">
    /// An optional factory function to create a custom <see cref="AI.ChatHistoryProvider"/> from its serialized state.
    /// If not provided, the default <see cref="InMemoryChatHistoryProvider"/> will be used.
    /// </param>
    /// <param name="aiContextProviderFactory">
    /// An optional factory function to create a custom <see cref="AIContextProvider"/> from its serialized state.
    /// If not provided, no context provider will be configured.
    /// </param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the deserialized <see cref="ChatClientAgentSession"/>.</returns>
    internal static async Task<ClaudeCodeAgentSession> DeserializeAsync(
        JsonElement serializedSessionState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        Func<JsonElement, JsonSerializerOptions?, CancellationToken, ValueTask<ChatHistoryProvider>>? chatHistoryProviderFactory = null,
        Func<JsonElement, JsonSerializerOptions?, CancellationToken, ValueTask<AIContextProvider>>? aiContextProviderFactory = null,
        CancellationToken cancellationToken = default)
    {
        if (serializedSessionState.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The serialized session state must be a JSON object.", nameof(serializedSessionState));
        }


        var state = serializedSessionState.Deserialize(
            AgentJsonUtilities.DefaultOptions.GetTypeInfo(typeof(SessionState))
            ) 
            as SessionState;

        var session = new ClaudeCodeAgentSession();

        session.AIContextProvider = aiContextProviderFactory is not null
            ? await aiContextProviderFactory.Invoke(state?.AIContextProviderState ?? default, jsonSerializerOptions, cancellationToken).ConfigureAwait(false)
            : null;

        if (state?.ConversationId is Guid conversationId)
        {
            session.ConversationId = conversationId;

            // Since we have an ID, we should not have a ChatHistoryProvider and we can return here.
            return session;
        }

        session._chatHistoryProvider =
            chatHistoryProviderFactory is not null
                ? await chatHistoryProviderFactory.Invoke(state?.ChatHistoryProviderState ?? default, jsonSerializerOptions, cancellationToken).ConfigureAwait(false)
                : new InMemoryChatHistoryProvider(state?.ChatHistoryProviderState ?? default, jsonSerializerOptions); // default to an in-memory ChatHistoryProvider

        return session;
    }

    /// <inheritdoc/>
    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        JsonElement? chatHistoryProviderState = this._chatHistoryProvider?.Serialize(jsonSerializerOptions);

        JsonElement? aiContextProviderState = this.AIContextProvider?.Serialize(jsonSerializerOptions);

        var state = new SessionState
        {
            ConversationId = this.ConversationId,
            ChatHistoryProviderState = chatHistoryProviderState is { ValueKind: not JsonValueKind.Undefined } ? chatHistoryProviderState : null,
            AIContextProviderState = aiContextProviderState is { ValueKind: not JsonValueKind.Undefined } ? aiContextProviderState : null,
        };

        return JsonSerializer.SerializeToElement(state, AgentJsonUtilities.DefaultOptions.GetTypeInfo(typeof(SessionState)));
    }

    /// <inheritdoc/>
    public override object? GetService(Type serviceType, object? serviceKey = null) =>
        base.GetService(serviceType, serviceKey)
            ?? this.AIContextProvider?.GetService(serviceType, serviceKey)
            ?? this.ChatHistoryProvider?.GetService(serviceType, serviceKey);


    internal sealed class SessionState
    {
        public Guid? ConversationId { get; set; }

        public JsonElement? ChatHistoryProviderState { get; set; }

        public JsonElement? AIContextProviderState { get; set; }
    }



    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay =>
        this.ConversationId is { } conversationId ? $"ConversationId = {conversationId}" :
        this._chatHistoryProvider is InMemoryChatHistoryProvider inMemoryChatHistoryProvider ? $"Count = {inMemoryChatHistoryProvider.Count}" :
        this._chatHistoryProvider is { } chatHistoryProvider ? $"ChatHistoryProvider = {chatHistoryProvider.GetType().Name}" :
        "Count = 0";
}
