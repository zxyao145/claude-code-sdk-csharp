using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;
using Microsoft.Extensions.AI;
using Xunit;

namespace ClaudeCodeSdk.Tests;

public class MafResponseTextTests
{
    [Fact]
    public async Task ProcessNonStreamingMessagesAsync_InitAssistantAndResult_TextIsNotDuplicated()
    {
        // Arrange
        using var agent = new ClaudeCodeAIAgent();
        var session = new ClaudeCodeAgentSession();
        var systemMessage = new SystemMessage
        {
            Id = "system-1",
            Subtype = "init",
            SessionId = "session-1",
            Data = new Dictionary<string, object> { ["model"] = "claude-test" },
        };
        var assistantMessage = new AssistantMessage
        {
            Id = "assistant-1",
            Model = "claude-test",
            SessionId = "session-1",
            Content = [new TextBlock { Text = "Hello" }],
        };
        var resultMessage = new ResultMessage
        {
            Id = "result-1",
            Subtype = "success",
            DurationMs = 1,
            DurationApiMs = 1,
            IsError = false,
            NumTurns = 1,
            SessionId = "session-1",
            Result = "Hello",
        };

        // Act
        var response = await agent.ProcessNonStreamingMessagesAsync(
            Messages(systemMessage, assistantMessage, resultMessage),
            session,
            [],
            TestContext.Current.CancellationToken
        );

        // Assert: the assistant's "Hello" is the only place the answer text appears.
        Assert.Equal("Hello", response.Text);

        var resultChatMessage = Assert.Single(
            response.Messages,
            message =>
                message.AdditionalProperties?.TryGetValue("type", out var type) == true
                && type as string == "result"
        );
        Assert.Equal("Hello", resultChatMessage.AdditionalProperties!["result"]);
        Assert.Equal(false, resultChatMessage.AdditionalProperties["isError"]);

        var systemChatMessage = Assert.Single(
            response.Messages,
            message => message.Role == ChatRole.System
        );
        Assert.Same(systemMessage.Data, systemChatMessage.AdditionalProperties!["systemData"]);
    }

    [Fact]
    public void ToAgentRunResponseUpdate_SuccessSubtypeButErrorResult_ReturnsErrorContentAndIsErrorTrue()
    {
        // Arrange
        IMessage message = new ResultMessage
        {
            Id = "result-1",
            Subtype = "success",
            DurationMs = 1,
            DurationApiMs = 1,
            IsError = true,
            NumTurns = 1,
            SessionId = "session-1",
            Result = "boom",
        };

        // Act
        var update = message.ToAgentRunResponseUpdate();

        // Assert
        Assert.NotNull(update);
        var error = Assert.IsType<ErrorContent>(Assert.Single(update.Contents));
        Assert.Equal("boom", error.Message);
        Assert.Equal(true, update.AdditionalProperties!["isError"]);
    }

    [Fact]
    public void ToAgentRunResponseUpdate_StructuredOutputResult_TextContentIsStructuredJson()
    {
        // Arrange
        const string json = """
            {
              "type":"result","uuid":"result-1","subtype":"success",
              "duration_ms":10,"duration_api_ms":5,"is_error":false,
              "num_turns":1,"session_id":"session-1","result":"plain text",
              "usage":{"input_tokens":10,"output_tokens":2},
              "structured_output":{"answer":"hello"}
            }
            """;
        var message = Assert.IsType<ResultMessage>(MessageParser.ParseMessage(json));

        // Act
        var update = message.ToAgentRunResponseUpdate();

        // Assert
        Assert.NotNull(update);
        var text = Assert.Single(update.Contents.OfType<TextContent>());
        Assert.Equal("{\"answer\":\"hello\"}", text.Text);
        Assert.False(update.AdditionalProperties!.ContainsKey("result"));
    }

    private static async IAsyncEnumerable<IMessage> Messages(params IMessage[] messages)
    {
        await Task.CompletedTask;
        foreach (var message in messages)
        {
            yield return message;
        }
    }
}
