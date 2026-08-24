using Microsoft.Extensions.AI;

namespace ClaudeCodeSdk.MAF;

internal static class ClaudeMafPromptBuilder
{
    public static object? Create(IEnumerable<ChatMessage> messages, string sessionId)
    {
        var message = messages.FirstOrDefault(static message => message.Role == ChatRole.User);
        if (message is null)
        {
            return null;
        }

        var hasImage = message.Contents.Any(static content =>
            content is DataContent data && data.HasTopLevelMediaType("image")
        );
        if (!hasImage)
        {
            return string.IsNullOrWhiteSpace(message.Text) ? null : message.Text;
        }

        var blocks = new List<Dictionary<string, object>>();
        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case DataContent data when data.HasTopLevelMediaType("image"):
                    blocks.Add(
                        new Dictionary<string, object>
                        {
                            ["type"] = "image",
                            ["source"] = new Dictionary<string, object>
                            {
                                ["type"] = "base64",
                                ["media_type"] = data.MediaType,
                                ["data"] = Convert.ToBase64String(data.Data.Span),
                            },
                        }
                    );
                    break;

                case TextContent text when !string.IsNullOrWhiteSpace(text.Text):
                    blocks.Add(
                        new Dictionary<string, object> { ["type"] = "text", ["text"] = text.Text }
                    );
                    break;
            }
        }

        return CreateMessageStream(
            new Dictionary<string, object>
            {
                ["type"] = "user",
                ["message"] = new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["content"] = blocks,
                },
                ["parent_tool_use_id"] = null!,
                ["session_id"] = sessionId,
            }
        );
    }

    private static async IAsyncEnumerable<Dictionary<string, object>> CreateMessageStream(
        Dictionary<string, object> message
    )
    {
        await Task.CompletedTask;
        yield return message;
    }
}
