using ClaudeCodeSdk.Types;

using Microsoft.Extensions.AI;

namespace ClaudeCodeSdk.MAF;

internal static partial class IMessageExtension
{
    public static ChatMessage? ToChatMessage(this IMessage message)
    {
        return message switch
        {
            AssistantMessage or SystemMessage or UserMessage or ResultMessage =>
                message.ToAgentRunResponseUpdate()?.ToChatMessage(),
            _ => null,
        };

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
    }
}
