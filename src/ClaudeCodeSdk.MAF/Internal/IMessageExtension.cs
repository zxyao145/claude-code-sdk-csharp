using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ClaudeCodeSdk.MAF;

internal static partial class IMessageExtension
{
    public static AgentRunResponseUpdate? ToAgentRunResponseUpdate(this IMessage claudeMessage)
    {
        if (claudeMessage is AssistantMessage assistantMsg
            && assistantMsg.Content.Count > 0)
        {
            var res = new AgentRunResponseUpdate
            {
                MessageId = claudeMessage.Id,
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
                MessageId = claudeMessage.Id,
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
                MessageId = claudeMessage.Id,
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
                    MessageId = claudeMessage.Id,
                    Role = ChatRole.System,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "type", claudeMessage.Type.Value },
                        { "subtype", resultMessage.Subtype },
                        { "totalCostUsd", resultMessage.TotalCostUsd  }
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
                var res = new FunctionResultContent(toolResultBlock.ToolUseId, toolResultBlock.ToolUseResult);
                aiContents.Add(res);
            }
        }

        return aiContents;
    }

    public static UsageDetails? ToUsageDetails(this ResultMessage resultMessage)
    {
        return ConvertUsageDetails(resultMessage);
    }

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
}
