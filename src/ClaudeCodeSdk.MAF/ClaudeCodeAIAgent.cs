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
        return NewThread();
    }

    private ClaudeCodeAgentThread NewThread()
    {
        return new ClaudeCodeAgentThread(sessionId: _options.Resume);
    }

    public override async Task<AgentRunResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentThread? thread = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default
        )
    {
        var claudeThread = thread as ClaudeCodeAgentThread ?? null;
        var messagesList = messages.ToList();
        var clientOptions = PrepareOptionsWithThread(claudeThread, messagesList);
        await using var client = new ClaudeSdkClient(clientOptions, _logger);

        try
        {
            // Connect to Claude
            await client.ConnectAsync(cancellationToken: cancellationToken);

            // Convert messages to Claude format and send (exclude System messages)
            var combinedMessages = messagesList
                .Where(m => m.Role == ChatRole.User)
                .ToList();


            // Receive and collect all responses
            var responseMessages = new List<ChatMessage>();

            foreach (var message in combinedMessages)
            {
                if (message.Role == ChatRole.User)
                {
                    var content = message.Text ?? string.Empty;
                    if(claudeThread  != null)
                    {
                        await client.QueryAsync(content,
                            sessionId: claudeThread.SessionId,
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await client.QueryAsync(content,
                            cancellationToken: cancellationToken);
                    }

                    await foreach (var claudeMessage in client.ReceiveResponseAsync(cancellationToken))
                    {
                        if (claudeMessage is AssistantMessage assistantMsg)
                        {
                            var textContent = ExtractTextFromAssistantMessage(assistantMsg);
                            if (!string.IsNullOrEmpty(textContent))
                            {
                                var assistantMessage = new ChatMessage(ChatRole.Assistant, textContent);
                                responseMessages.Add(assistantMessage);
                                if (claudeThread != null)
                                {
                                    claudeThread.Messages.Add(assistantMessage);
                                }
                            }
                        }
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
        var claudeThread = thread as ClaudeCodeAgentThread ?? null;
        var messagesList = messages.ToList();
        var clientOptions = PrepareOptionsWithThread(claudeThread, messagesList);
        clientOptions = clientOptions with { Resume = null };

        await using var client = new ClaudeSdkClient(clientOptions, _logger);

        // Connect to Claude
        await client.ConnectAsync(cancellationToken: cancellationToken);

        // Convert messages to Claude format and send (exclude System messages)
        var combinedMessages = messagesList
            .Where(m => m.Role == ChatRole.User)
            .ToList();

        foreach (var message in messagesList)
        {
            var content = message.Text ?? string.Empty;
            if (claudeThread != null)
            {
                await client.QueryAsync(content,
                    sessionId: claudeThread.SessionId,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await client.QueryAsync(content,
                    cancellationToken: cancellationToken);
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
                if (claudeThread != null &&
                        claudeMessage is AssistantMessage assistantMsg)
                {
                    var textContent = ExtractTextFromAssistantMessage(assistantMsg);
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        claudeThread.Messages.Add(new ChatMessage(ChatRole.Assistant, textContent));
                    }
                }
            }
        }
    }

    private AgentRunResponseUpdate? ConvertToAgentRunResponseUpdate(IMessage claudeMessage)
    {
        if (claudeMessage is AssistantMessage assistantMsg
            && assistantMsg.Content.Count > 0)
        {
            var res = new AgentRunResponseUpdate
            {
                Role = ChatRole.Assistant,
                ResponseId = Guid.NewGuid().ToString(),
                MessageId = Guid.NewGuid().ToString(),
                AuthorName = assistantMsg.Model,
            };
            ConvertContent(assistantMsg.Content, res);
            return res;
        }

        if (claudeMessage is SystemMessage systemMessage)
        {
            return new AgentRunResponseUpdate
            {
                Role = ChatRole.System,
                AuthorName = systemMessage.Subtype,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { "subtype" , systemMessage.Subtype}
                },
                Contents = [new TextContent($"[{JsonUtil.Serialize(systemMessage.Data)}")],
                ResponseId = Guid.NewGuid().ToString(),
                MessageId = Guid.NewGuid().ToString()
            };
        }

        if (claudeMessage is UserMessage userMessage)
        {
            var res = new AgentRunResponseUpdate
            {
                Role = ChatRole.User,
                ResponseId = Guid.NewGuid().ToString(),
                MessageId = Guid.NewGuid().ToString()
            };

            // Handle Content which can be string or List<IContentBlock>
            if (userMessage.Content is string str)
            {
                res.Contents = [new TextContent($"{str}")];
            }
            else if (userMessage.Content is IEnumerable<IContentBlock> blocks)
            {
                ConvertContent(blocks, res);
            }
            else
            {
                res.Contents = [new TextContent($"{userMessage.Content?.ToString() ?? string.Empty}")];
            }

            return res;
        }

        if (claudeMessage is ResultMessage resultMessage)
        {
            return new AgentRunResponseUpdate
            {
                Role = ChatRole.System,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { "type", "result" },
                    { "subtype", resultMessage.Subtype },
                },
                Contents = [new TextContent($"{JsonUtil.Serialize(resultMessage)}")],
                ResponseId = Guid.NewGuid().ToString(),
                MessageId = Guid.NewGuid().ToString()
            };
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
                textParts.Add($"Thinking: {thinkingBlock.Thinking}");
            }
        }

        return JsonUtil.Serialize(textParts);
        // return string.Join("\n", textParts);
    }

    private static void ConvertContent(IEnumerable<IContentBlock> contents, AgentRunResponseUpdate result)
    {
        var aiContents = new List<AIContent>();
        result.Contents = aiContents;

        foreach (var item in contents)
        {
            if (item is TextBlock textBlock)
            {
                aiContents.Add(
                    new TextContent($"{textBlock.Text}")
                );
            }

            if (item is ThinkingBlock thinkingBlock)
            {
                aiContents.Add(
                    new TextContent($"{"Thinking: " + thinkingBlock.Thinking}")
                );
            }

            if (item is ToolUseBlock toolUseBlock)
            {
                aiContents.Add(
                    new TextContent($"{"Using Tool:" + toolUseBlock.Name}")
                );
            }

            if (item is ToolResultBlock toolResultBlock)
            {
                aiContents.Add(
                    new TextContent($"{$"Using Result:" + toolResultBlock.Content}")
                );
            }
        }
    }

    private ClaudeCodeOptions PrepareOptionsWithThread(ClaudeCodeAgentThread? thread, IEnumerable<ChatMessage> messages)
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

        if (thread == null)
        {
            return options;
        }

        // If thread has a session ID, use it as Resume parameter
        if (!string.IsNullOrEmpty(thread.SessionId))
        {
            options = options with { Resume = thread.SessionId };
        }

        return options;
    }
}
