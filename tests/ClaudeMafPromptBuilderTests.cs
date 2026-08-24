using ClaudeCodeSdk.MAF;
using Microsoft.Extensions.AI;
using Xunit;

namespace ClaudeCodeSdk.Tests;

public class ClaudeMafPromptBuilderTests
{
    [Fact]
    public async Task Create_TextAndImage_PreservesOrderedClaudeBlocks()
    {
        var prompt = ClaudeMafPromptBuilder.Create(
            [
                new ChatMessage(
                    ChatRole.User,
                    [
                        new DataContent(new byte[] { 1, 2, 3 }, "image/png"),
                        new TextContent("describe this"),
                    ]
                ),
            ],
            "session-1"
        );

        var message = await GetSingleMessageAsync(prompt);
        Assert.Equal("session-1", message["session_id"]);
        var envelope = Assert.IsType<Dictionary<string, object>>(message["message"]);
        var blocks = Assert.IsType<List<Dictionary<string, object>>>(envelope["content"]);
        var image = blocks[0];
        var source = Assert.IsType<Dictionary<string, object>>(image["source"]);

        Assert.Equal("image", image["type"]);
        Assert.Equal("base64", source["type"]);
        Assert.Equal("image/png", source["media_type"]);
        Assert.Equal("AQID", source["data"]);
        Assert.Equal("text", blocks[1]["type"]);
        Assert.Equal("describe this", blocks[1]["text"]);
    }

    [Fact]
    public async Task Create_ImageOnly_ReturnsStreamJsonPrompt()
    {
        var prompt = ClaudeMafPromptBuilder.Create(
            [new ChatMessage(ChatRole.User, [new DataContent(new byte[] { 1 }, "image/webp")])],
            "default"
        );

        var message = await GetSingleMessageAsync(prompt);
        var envelope = Assert.IsType<Dictionary<string, object>>(message["message"]);
        var blocks = Assert.IsType<List<Dictionary<string, object>>>(envelope["content"]);

        Assert.Single(blocks);
        Assert.Equal("image", blocks[0]["type"]);
    }

    [Fact]
    public void Create_TextOnly_PreservesStringPrompt()
    {
        var prompt = ClaudeMafPromptBuilder.Create(
            [new ChatMessage(ChatRole.User, "hello")],
            "default"
        );

        Assert.Equal("hello", Assert.IsType<string>(prompt));
    }

    [Fact]
    public void Create_EmptyInput_ReturnsNull()
    {
        Assert.Null(ClaudeMafPromptBuilder.Create([], "default"));
    }

    private static async Task<Dictionary<string, object>> GetSingleMessageAsync(object? prompt)
    {
        var stream = Assert.IsAssignableFrom<IAsyncEnumerable<Dictionary<string, object>>>(prompt);
        var messages = new List<Dictionary<string, object>>();
        await foreach (var message in stream)
        {
            messages.Add(message);
        }

        return Assert.Single(messages);
    }
}
