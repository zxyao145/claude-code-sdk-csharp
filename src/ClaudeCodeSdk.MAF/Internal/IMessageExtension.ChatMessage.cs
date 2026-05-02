using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Microsoft.Extensions.AI;

namespace ClaudeCodeSdk.MAF;

internal static partial class IMessageExtension
{
    public static ChatMessage? ToChatMessage(this IMessage claudeMessage)
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
                )
                {
                    AuthorName = message.Model,
                    MessageId = message.Id,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "agentName", AGENT_NAME },
                        { "type", message.Type.Value }
                    }
                };
            return assistantMessage;
        }

        #region MyRegion

        //if (claudeMessage is SystemMessage systemMessage)
        //{
        //    return new ChatMessage
        //    {
        //        MessageId = claudeMessage.Id,
        //        Role = ChatRole.System,

        //        AdditionalProperties = new AdditionalPropertiesDictionary
        //        {
        //            { "agentName", AGENT_NAME },
        //            { "type", claudeMessage.Type.Value },
        //            { "subtype" , systemMessage.Subtype}
        //        },
        //        Contents = [new TextContent($"{JsonUtil.Serialize(systemMessage.Data)}")],
        //    };
        //}

        //if (claudeMessage is UserMessage userMessage)
        //{
        //    var res = new ChatMessage
        //    {
        //        MessageId = claudeMessage.Id,
        //        Role = ChatRole.User,
        //        AdditionalProperties = new AdditionalPropertiesDictionary
        //        {
        //            { "agentName", AGENT_NAME },
        //            { "type", claudeMessage.Type.Value },
        //        },
        //    };

        //    // Handle Content which can be string or List<IContentBlock>
        //    if (userMessage.Content is string str)
        //    {
        //        res.Contents = [new TextContent($"{str}")];
        //    }
        //    else if (userMessage.Content is IEnumerable<IContentBlock> blocks)
        //    {
        //        res.Contents = ConvertContent(blocks);
        //    }
        //    else
        //    {
        //        res.Contents = [new TextContent($"{userMessage.Content?.ToString() ?? string.Empty}")];
        //    }

        //    return res;
        //}

        if (claudeMessage is ResultMessage resultMessage)
        {
            UsageDetails? usageDetails = ConvertUsageDetails(resultMessage);
            if (usageDetails != null)
            {
                return new ChatMessage
                {
                    MessageId = claudeMessage.Id,
                    Role = ChatRole.System,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "agentName", AGENT_NAME },
                        { "type", claudeMessage.Type.Value },
                        { "subtype", resultMessage.Subtype },
                        { "totalCostUsd", resultMessage.TotalCostUsd  }
                    },
                    Contents = [new UsageContent(usageDetails)],
                };
            }
        }

        #endregion

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
            return $"Error: {errorBlock.Message}, Details: {errorBlock.Details}";
        }
        throw new NotImplementedException();
    }

}
