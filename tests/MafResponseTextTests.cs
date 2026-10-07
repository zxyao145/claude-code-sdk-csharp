using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;
using Microsoft.Agents.AI;
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
    public async Task ProcessStreamingMessagesAsync_AssistantAndResult_PreservesFinalResultText()
    {
        // 准备助手消息及最终结果。
        // Arrange the assistant message and final result.
        using var agent = new ClaudeCodeAIAgent();
        var session = new ClaudeCodeAgentSession();
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

        // 执行流式消息处理。
        // Act through the streaming message pipeline.
        var updates = new List<AgentResponseUpdate>();
        await foreach (
            var update in agent.ProcessStreamingMessagesAsync(
                Messages(assistantMessage, resultMessage),
                session,
                [],
                TestContext.Current.CancellationToken
            )
        )
        {
            updates.Add(update);
        }

        // 验证助手消息与最终结果各自携带正文。
        // Assert that the assistant message and final result each carry their text.
        Assert.Equal(2, updates.Count);
        Assert.Equal("assistant-1", updates[0].MessageId);
        Assert.Equal("Hello", Assert.IsType<TextContent>(Assert.Single(updates[0].Contents)).Text);
        var resultUpdate = updates[1];
        Assert.Equal("result-1", resultUpdate.MessageId);
        Assert.Equal(ChatRole.Assistant, resultUpdate.Role);
        Assert.Equal(
            "Hello",
            Assert.IsType<TextContent>(Assert.Single(resultUpdate.Contents)).Text
        );
        Assert.Equal("result", resultUpdate.AdditionalProperties!["type"]);
        Assert.Equal("success", resultUpdate.AdditionalProperties["subtype"]);
        Assert.Equal(false, resultUpdate.AdditionalProperties["isError"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Process_FinalResultWithHistory_PreservesTextForResponseMode(bool streaming)
    {
        // 准备最终结果及启用历史记录的消息处理器。
        // Arrange a final result and a processor with history persistence enabled.
        const string json = """
            {
              "type":"result","uuid":"result-1","subtype":"success",
              "duration_ms":1,"duration_api_ms":1,"is_error":false,
              "num_turns":1,"session_id":"session-1","result":"Hello"
            }
            """;
        var message = Assert.IsType<ResultMessage>(MessageParser.ParseMessage(json));
        var processor = new ClaudeStreamingMessageProcessor(
            [],
            enableHistoryPersistence: true,
            enableMessageMapping: streaming
        );

        // 处理消息并完成历史记录批次。
        // Act by processing the message and completing the history batch.
        processor.Process(message);
        var batch = processor.CompleteRun();

        // 验证历史记录使用调用方的响应模式。
        // Assert that history uses the caller's response mode.
        var update = Assert.Single(Assert.IsType<ClaudeHistoryBatch>(batch).ResponseUpdates);
        Assert.Equal("result", update.AdditionalProperties!["type"]);
        if (streaming)
        {
            Assert.Equal("Hello", Assert.IsType<TextContent>(Assert.Single(update.Contents)).Text);
        }
        else
        {
            Assert.Empty(update.Contents);
            Assert.Equal("Hello", update.AdditionalProperties["result"]);
        }
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
