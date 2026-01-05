using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Runtime.CompilerServices;
using ClaudeCodeSdk.Types;
using Microsoft.Extensions.Logging;

namespace ClaudeCodeSdk.MAF;

/// <summary>
/// AIAgent implementation using ClaudeCodeSdk for Claude Code interactions.
/// </summary>
public class ClaudeCodeAIAgent : AIAgent
{
    private readonly ClaudeCodeOptions _options;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialize Claude Code AI Agent.
    /// </summary>
    /// <param name="options">Optional ClaudeCodeOptions configuration</param>
    /// <param name="logger">Optional logger for debugging</param>
    public ClaudeCodeAIAgent(ClaudeCodeOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? new ClaudeCodeOptions();
        _logger = logger;
    }

    public override AgentThread DeserializeThread(JsonElement serializedThread, JsonSerializerOptions? jsonSerializerOptions = null)
    {
        var sessionId = serializedThread.TryGetProperty("sessionId", out var sidProp)
            ? sidProp.GetString() ?? "default"
            : "default";

        var messages = new List<ChatMessage>();
        if (serializedThread.TryGetProperty("messages", out var messagesProp) && messagesProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var msgElement in messagesProp.EnumerateArray())
            {
                if (msgElement.TryGetProperty("role", out var roleProp) &&
                    msgElement.TryGetProperty("content", out var contentProp))
                {
                    var role = roleProp.GetString();
                    var content = contentProp.GetString();

                    if (role == "user")
                    {
                        messages.Add(new ChatMessage(ChatRole.User, content));
                    }
                    else if (role == "assistant")
                    {
                        messages.Add(new ChatMessage(ChatRole.Assistant, content));
                    }
                }
            }
        }

        return new ClaudeCodeAgentThread(sessionId, messages);
    }

    public override AgentThread GetNewThread()
    {
        return new ClaudeCodeAgentThread();
    }

    public override async Task<AgentRunResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default
        )
    {
        var claudeThread = thread as ClaudeCodeAgentThread ?? new ClaudeCodeAgentThread();
        var messagesList = messages.ToList();
        var clientOptions = PrepareOptionsWithThread(claudeThread, messagesList);
        var client = new ClaudeSdkClient(clientOptions, _logger);

        try
        {
            // Connect to Claude
            await client.ConnectAsync(cancellationToken: cancellationToken);

            // Convert messages to Claude format and send (exclude System messages)
            var combinedMessages = claudeThread.Messages.Concat(messagesList.Where(m => m.Role != ChatRole.System)).ToList();

            foreach (var message in combinedMessages)
            {
                if (message.Role == ChatRole.User)
                {
                    var content = message.Text ?? string.Empty;
                    await client.QueryAsync(content, claudeThread.SessionId, cancellationToken);
                }
            }

            // Receive and collect all responses
            var responseMessages = new List<ChatMessage>();
            await foreach (var claudeMessage in client.ReceiveResponseAsync(cancellationToken))
            {
                if (claudeMessage is AssistantMessage assistantMsg)
                {
                    var textContent = ExtractTextFromAssistantMessage(assistantMsg);
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        var assistantMessage = new ChatMessage(ChatRole.Assistant, textContent);
                        responseMessages.Add(assistantMessage);
                        claudeThread.Messages.Add(assistantMessage);
                    }
                }
            }

            // Return complete response
            return new AgentRunResponse
            {
                Messages = responseMessages,
                ResponseId = Guid.NewGuid().ToString()
            };
        }
        finally
        {
            await client.DisconnectAsync(cancellationToken);
            await client.DisposeAsync();
        }
    }

    public override async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var claudeThread = thread as ClaudeCodeAgentThread ?? new ClaudeCodeAgentThread();
        var messagesList = messages.ToList();
        var clientOptions = PrepareOptionsWithThread(claudeThread, messagesList);
        var client = new ClaudeSdkClient(clientOptions, _logger);

        try
        {
            // Connect to Claude
            await client.ConnectAsync(cancellationToken: cancellationToken);

            // Convert messages to Claude format and send (exclude System messages)
            var combinedMessages = claudeThread.Messages.Concat(messagesList.Where(m => m.Role != ChatRole.System)).ToList();

            foreach (var message in combinedMessages)
            {
                if (message.Role == ChatRole.User)
                {
                    var content = message.Text ?? string.Empty;
                    await client.QueryAsync(content, claudeThread.SessionId, cancellationToken);
                }
            }

            // Receive and yield responses
            await foreach (var claudeMessage in client.ReceiveResponseAsync(cancellationToken))
            {
                var update = ConvertToAgentRunResponseUpdate(claudeMessage);
                if (update != null)
                {
                    yield return update;
                }

                // Update thread with response
                if (claudeMessage is AssistantMessage assistantMsg)
                {
                    var textContent = ExtractTextFromAssistantMessage(assistantMsg);
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        claudeThread.Messages.Add(new ChatMessage(ChatRole.Assistant, textContent));
                    }
                }
            }
        }
        finally
        {
            await client.DisconnectAsync(cancellationToken);
            await client.DisposeAsync();
        }
    }

    private AgentRunResponseUpdate? ConvertToAgentRunResponseUpdate(IMessage claudeMessage)
    {
        if (claudeMessage is AssistantMessage assistantMsg)
        {
            var textContent = ExtractTextFromAssistantMessage(assistantMsg);
            if (!string.IsNullOrEmpty(textContent))
            {
                return new AgentRunResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent(textContent)],
                    ResponseId = Guid.NewGuid().ToString(),
                    MessageId = Guid.NewGuid().ToString()
                };
            }
        }

        return null;
    }

    private string ExtractTextFromAssistantMessage(AssistantMessage message)
    {
        var textParts = new List<string>();

        foreach (var content in message.Content)
        {
            if (content is TextBlock textBlock)
            {
                textParts.Add(textBlock.Text);
            }
            else if (content is ThinkingBlock thinkingBlock)
            {
                // Optionally include thinking content
                // textParts.Add($"[Thinking: {thinkingBlock.Thinking}]");
            }
        }

        return string.Join("\n", textParts);
    }

    private ClaudeCodeOptions PrepareOptionsWithThread(ClaudeCodeAgentThread thread, IEnumerable<ChatMessage> messages)
    {
        var options = _options;

        // Extract system prompt from messages if present
        var systemMessage = messages.FirstOrDefault(m => m.Role == ChatRole.System);
        if (systemMessage != null)
        {
            var systemPrompt = systemMessage.Text;
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                options = options with { SystemPrompt = systemPrompt };
            }
        }

        // If thread has a session ID, use it as Resume parameter
        if (!string.IsNullOrEmpty(thread.SessionId))
        {
            options = options with { Resume = thread.SessionId };
        }

        return options;
    }
}
