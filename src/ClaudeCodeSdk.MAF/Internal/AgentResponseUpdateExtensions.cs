using System.Diagnostics.CodeAnalysis;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ClaudeCodeSdk.MAF;

internal static class AgentResponseUpdateExtensions
{
    public static ChatMessage ToChatMessage([NotNull] this AgentResponseUpdate update)
    {
        var chatMessage = new ChatMessage()
        {
            AdditionalProperties = update.AdditionalProperties,
            AuthorName = update.AuthorName,
            Contents = update.Contents,
            CreatedAt = update.CreatedAt,
            MessageId = update.MessageId,
            Role = update.Role ?? ChatRole.System,
            RawRepresentation = update.RawRepresentation,
        };

        return chatMessage;
    }
}
