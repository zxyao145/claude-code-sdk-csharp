using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;

using Microsoft.Extensions.AI;

using Xunit;

namespace ClaudeCodeSdk.Tests;

public class MafErrorContentTests
{
    [Fact]
    public void ToAgentRunResponseUpdate_ErrorResult_ReturnsTerminalErrorAndUsage()
    {
        var message = CreateResultMessage("quota exceeded", new Usage
        {
            InputTokens = 10,
            OutputTokens = 2,
        });

        var update = message.ToAgentRunResponseUpdate();

        Assert.NotNull(update);
        var error = Assert.IsType<ErrorContent>(update.Contents[0]);
        Assert.Equal("quota exceeded", error.Message);
        Assert.True(Assert.IsType<bool>(error.AdditionalProperties!["isTerminalError"]));
        Assert.IsType<UsageContent>(update.Contents[1]);
    }

    [Fact]
    public void ToAgentRunResponseUpdate_ErrorResultWithoutText_UsesSubtypeFallback()
    {
        var message = CreateResultMessage(null);

        var update = message.ToAgentRunResponseUpdate();

        var error = Assert.IsType<ErrorContent>(Assert.Single(update!.Contents));
        Assert.Equal("Claude Code execution failed: execution_error.", error.Message);
    }

    [Fact]
    public void ParseMessage_FailedToolResult_PreservesToolResultMetadata()
    {
        const string json = """
            {
              "type": "assistant",
              "uuid": "assistant-1",
              "session_id": "session-1",
              "message": {
                "model": "claude-test",
                "content": [
                  {
                    "type": "tool_result",
                    "tool_use_id": "tool-1",
                    "content": "boom",
                    "is_error": true
                  }
                ]
              },
              "tool_use_result": { "detail": "boom" }
            }
            """;

        var message = Assert.IsType<AssistantMessage>(MessageParser.ParseMessage(json));

        var toolResult = Assert.IsType<ToolResultBlock>(Assert.Single(message.Content));
        Assert.Equal("tool-1", toolResult.ToolUseId);
        Assert.True(toolResult.IsError);

        var update = message.ToAgentRunResponseUpdate();
        var error = Assert.IsType<ErrorContent>(update!.Contents[1]);
        Assert.Equal("boom", error.Message);
    }

    [Fact]
    public void ParseMessage_SuccessfulToolResultWithoutErrorFlag_PreservesNullErrorState()
    {
        const string json = """
            {
              "type": "assistant",
              "uuid": "assistant-1",
              "session_id": "session-1",
              "message": {
                "model": "claude-test",
                "content": [
                  {
                    "type": "tool_result",
                    "tool_use_id": "tool-1",
                    "content": "done"
                  }
                ]
              },
              "tool_use_result": { "detail": "done" }
            }
            """;

        var message = Assert.IsType<AssistantMessage>(MessageParser.ParseMessage(json));

        var toolResult = Assert.IsType<ToolResultBlock>(Assert.Single(message.Content));
        Assert.Null(toolResult.IsError);
    }

    [Fact]
    public void ToChatMessage_SuccessfulAssistantMessage_PreservesExistingNonStreamingFormat()
    {
        var message = new AssistantMessage
        {
            Id = "assistant-1",
            Model = "claude-test",
            SessionId = "session-1",
            Content = [new TextBlock { Text = "hello" }],
        };

        var chatMessage = message.ToChatMessage();

        var text = Assert.IsType<TextContent>(Assert.Single(chatMessage!.Contents));
        Assert.Equal("[\"hello\"]", text.Text);
    }

    [Fact]
    public void ToAgentRunResponseUpdate_FailedToolResult_ReturnsFunctionResultAndError()
    {
        var message = new AssistantMessage
        {
            Id = "assistant-1",
            Model = "claude-test",
            SessionId = "session-1",
            Content =
            [
                new ToolResultBlock
                {
                    ToolUseId = "tool-1",
                    Content = "boom",
                    ToolUseResult = new Dictionary<string, object> { ["detail"] = "boom" },
                    IsError = true,
                },
            ],
        };

        var update = message.ToAgentRunResponseUpdate();

        Assert.NotNull(update);
        var result = Assert.IsType<FunctionResultContent>(update.Contents[0]);
        Assert.Equal("tool-1", result.CallId);
        var error = Assert.IsType<ErrorContent>(update.Contents[1]);
        Assert.Equal("boom", error.Message);
        Assert.Null(error.AdditionalProperties);
    }

    private static ResultMessage CreateResultMessage(string? result, Usage? usage = null) =>
        new()
        {
            Id = "result-1",
            Subtype = "execution_error",
            DurationMs = 100,
            DurationApiMs = 90,
            IsError = true,
            NumTurns = 1,
            SessionId = "session-1",
            Result = result,
            Usage = usage,
        };
}
