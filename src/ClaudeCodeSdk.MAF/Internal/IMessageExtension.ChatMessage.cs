using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;

using Microsoft.Extensions.AI;

namespace ClaudeCodeSdk.MAF;

internal static partial class IMessageExtension
{
    public static ChatMessage ToChatMessage(this AssistantMessage message)
    {
        var textParts = message.Content
            .Select(ConvertContentBlockToString)
            .ToList();

        return new ChatMessage(
            ChatRole.Assistant,
            [new TextContent(JsonUtil.Serialize(textParts))])
        {
            AuthorName = message.Model,
            MessageId = message.Id,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "agentName", AGENT_NAME },
                { "type", message.Type.Value },
            },
        };
    }

    private static string ConvertContentBlockToString(IContentBlock content)
    {
        return content switch
        {
            TextBlock textBlock => textBlock.Text,
            ThinkingBlock thinkingBlock => $"Thinking: {thinkingBlock.Thinking}",
            ToolUseBlock toolUseBlock => $"Tool using: {toolUseBlock.Name}",
            ToolResultBlock toolResultBlock =>
                $"Tool use result: {(toolResultBlock.IsError == true ? "error" : "success")}",
            ErrorContentBlock errorBlock =>
                $"Error: {errorBlock.Message}, Details: {errorBlock.Details}",
            _ => throw new NotImplementedException(),
        };
    }
}
