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
/// </summary>
public class ClaudeCodeAIAgent : AIAgent
{
    private readonly ClaudeCodeAIAgentOptions _options;
    private readonly ILogger? _logger;

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
            UsageDetails? usageDetails = null;
            foreach (var message in combinedMessages)
            {
                if (message.Role == ChatRole.User)
                {
                    var content = message.Text ?? string.Empty;
                    await client.QueryAsync(content,
                             sessionId: claudeThread.SessionId,
                             cancellationToken: cancellationToken);

                    await foreach (var claudeMessage in client.ReceiveResponseAsync(cancellationToken))
                    {
                        claudeThread.SetSessionIdIfNull(claudeMessage);

                        var assistantMessage = ConvertClaudeMessageToChatMessage(claudeMessage);
                        if (assistantMessage != null)
                        {
                            responseMessages.Add(assistantMessage);
                        }

                        if (claudeMessage is ResultMessage resultMessage)
                        {
                            usageDetails = ConvertUsageDetails(resultMessage);
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
        finally
        {
            await client.DisconnectAsync();
            await client.DisposeAsync();
        }
    }

    private ChatMessage? ConvertClaudeMessageToChatMessage(IMessage claudeMessage)
    {
        if (claudeMessage is AssistantMessage message)
        {
            var textParts = new List<string>();

            foreach (var content in message.Content)
            {
                textParts.Add(ConvertContentBlockToString(content));
            }

            ChatMessage assistantMessage =
                new ChatMessage(
                    ChatRole.Assistant,
                    [new TextContent(JsonUtil.Serialize(textParts))]
                );
            return assistantMessage;
        }

        return null;
    }

    private static string ConvertContentBlockToString(IContentBlock content)
    {
        if (content is TextBlock textBlock)
        {
            return textBlock.Text;
        }
        else if (content is ThinkingBlock thinkingBlock)
        {
            return $"Thinking: {thinkingBlock.Thinking}";
            // Optionally include thinking content
        }
        else if (content is ToolUseBlock toolUseBlock)
        {
            return $"Tool using: {toolUseBlock.Name}";
        }
        else if (content is ToolResultBlock toolResultBlock)
        {
            var res = (toolResultBlock.IsError ?? false) ? "error" : "success";
            return $"Tool use result: {res}";
        }
        else if (content is ErrorContentBlock errorBlock)
        {
            return $"Error: {errorBlock.Message}";
        }
        throw new NotImplementedException();
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
        var clientOptions = PrepareOptionsWithThread(claudeThread, messagesList);

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
            await client.QueryAsync(content,
                     sessionId: claudeThread.SessionId,
                     cancellationToken: cancellationToken);

            // Receive and yield responses
            await foreach (var claudeMessage in client.ReceiveResponseAsync(cancellationToken))
            {
                claudeThread.SetSessionIdIfNull(claudeMessage);

                var update = ConvertToAgentRunResponseUpdate(claudeMessage);
                if (update != null)
                {
                    yield return update;
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
                AuthorName = assistantMsg.Model,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { "type", claudeMessage.Type.Value },
                },
            };
            res.Contents = ConvertContent(assistantMsg.Content);
            return res;
        }

        if (claudeMessage is SystemMessage systemMessage)
        {
            return new AgentRunResponseUpdate
            {
                Role = ChatRole.System,

                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { "type", claudeMessage.Type.Value },
                    { "subtype" , systemMessage.Subtype}
                },
                Contents = [new TextContent($"{JsonUtil.Serialize(systemMessage.Data)}")],
            };
        }

        if (claudeMessage is UserMessage userMessage)
        {
            var res = new AgentRunResponseUpdate
            {
                Role = ChatRole.User,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { "type", claudeMessage.Type.Value },
                },
            };

            // Handle Content which can be string or List<IContentBlock>
            if (userMessage.Content is string str)
            {
                res.Contents = [new TextContent($"{str}")];
            }
            else if (userMessage.Content is IEnumerable<IContentBlock> blocks)
            {
                res.Contents = ConvertContent(blocks);
            }
            else
            {
                res.Contents = [new TextContent($"{userMessage.Content?.ToString() ?? string.Empty}")];
            }

            return res;
        }

        if (claudeMessage is ResultMessage resultMessage)
        {
            UsageDetails? usageDetails = ConvertUsageDetails(resultMessage);
            if (usageDetails != null)
            {
                return new AgentRunResponseUpdate
                {
                    Role = ChatRole.System,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "type", claudeMessage.Type.Value },
                        { "subtype", resultMessage.Subtype },
                    },
                    Contents = [new UsageContent(usageDetails)],
                };
            }
        }

        return null;
    }

    private static List<AIContent> ConvertContent(IEnumerable<IContentBlock> contents)
    {
        var aiContents = new List<AIContent>();

        foreach (var item in contents)
        {
            if (item is TextBlock textBlock)
            {
                aiContents.Add(
                    new TextContent($"{textBlock.Text}")
                );
            }
            if (item is ErrorContentBlock errorBlock)
            {
                var res = new ErrorContent(errorBlock.Message);
                aiContents.Add(res);
            }
            if (item is ThinkingBlock thinkingBlock)
            {
                var res = new TextReasoningContent(thinkingBlock.Thinking);
                aiContents.Add(res);
            }

            if (item is ToolUseBlock toolUseBlock)
            {
                Dictionary<string, object?> arguments = toolUseBlock.Input
                    .ToDictionary(x => x.Key, x => (object?)x.Value);
                var funcall = new FunctionCallContent(toolUseBlock.Id, toolUseBlock.Name, arguments);
                aiContents.Add(funcall);
            }

            if (item is ToolResultBlock toolResultBlock)
            {
                var res = new FunctionResultContent(toolResultBlock.ToolUseId, toolResultBlock.Content);
                aiContents.Add(res);
            }
        }

        return aiContents;
    }

    #endregion


    private static UsageDetails? ConvertUsageDetails(ResultMessage resultMessage)
    {
        var usage = resultMessage.Usage;
        if (usage == null)
        {
            return null;
        }

        var usageDetails = new UsageDetails
        {
            InputTokenCount = usage.InputTokens,
            OutputTokenCount = usage.OutputTokens,
            CachedInputTokenCount = usage.CacheCreationInputTokens,
            AdditionalCounts = new AdditionalPropertiesDictionary<long>
            {
                {"cacheReadInputTokens", usage.CacheReadInputTokens },
            }
        };

        return usageDetails;
    }


    private ClaudeCodeOptions PrepareOptionsWithThread(ClaudeCodeAgentThread? thread, IEnumerable<ChatMessage> messages)
    {
        var options = _options.ToClaudeCodeOptions();

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