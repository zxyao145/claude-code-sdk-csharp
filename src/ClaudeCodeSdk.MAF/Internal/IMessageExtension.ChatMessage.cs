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
            return $"Error: {errorBlock.Message}, Details: {errorBlock.Details}";
        }
        throw new NotImplementedException();
    }

}