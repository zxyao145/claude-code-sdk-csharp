using System.Text.Json;
using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;
using Microsoft.Extensions.AI;
using Xunit;

namespace ClaudeCodeSdk.Tests;

public class MafErrorContentTests
{
    [Fact]
    public void ToAgentRunResponseUpdate_ErrorResult_ReturnsFatalErrorAndUsage()
    {
        var message = CreateResultMessage(
            "quota exceeded",
            new Usage { InputTokens = 10, OutputTokens = 2 }
        );

        var update = message.ToAgentRunResponseUpdate();

        Assert.NotNull(update);
        var error = Assert.IsType<ErrorContent>(update.Contents[0]);
        Assert.Equal("quota exceeded", error.Message);
        Assert.True(Assert.IsType<bool>(error.AdditionalProperties!["isFatalError"]));
        Assert.IsType<UsageContent>(update.Contents[1]);
        Assert.Equal("claude-code", update.AuthorName);
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
    public void ToAgentRunResponseUpdate_ApiRetrySystemMessage_ReturnsRecoverableError()
    {
        const string json = """
            {
              "type": "system",
              "subtype": "api_retry",
              "attempt": 5,
              "max_retries": 10,
              "retry_delay_ms": 8341.295757881328,
              "error_status": null,
              "error": "unknown",
              "session_id": "0613affc-4694-4885-a301-2a6c343aa6fa",
              "uuid": "968472f0-c4f8-4930-88b6-a35f0ced10c1"
            }
            """;

        var message = Assert.IsType<SystemMessage>(MessageParser.ParseMessage(json));

        var update = message.ToAgentRunResponseUpdate();

        var error = Assert.IsType<ErrorContent>(Assert.Single(update!.Contents));
        Assert.Equal("Claude Code API retry 5/10: unknown", error.Message);
        Assert.Null(error.AdditionalProperties);
        Assert.Equal("claude-code", update.AuthorName);
        Assert.Equal("claude-code", update.AdditionalProperties!["agentName"]);
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
    public void ParseMessage_UserToolResultArray_PreservesArbitraryJsonForMaf()
    {
        const string json = """
            {
              "type": "user",
              "uuid": "user-1",
              "message": {
                "content": [
                  {
                    "type": "tool_result",
                    "tool_use_id": "mcp-tool-1",
                    "content": [
                      { "type": "text", "text": "exploration result" }
                    ]
                  }
                ]
              },
              "tool_use_result": [
                { "type": "text", "text": "exploration result" }
              ]
            }
            """;

        var message = Assert.IsType<UserMessage>(MessageParser.ParseMessage(json));

        var contentBlocks = Assert.IsType<List<IContentBlock>>(message.Content);
        var toolResult = Assert.IsType<ToolResultBlock>(Assert.Single(contentBlocks));
        var parsedResult = Assert.IsType<JsonElement>(toolResult.ToolUseResult);
        Assert.Equal(JsonValueKind.Array, parsedResult.ValueKind);
        Assert.Equal("exploration result", parsedResult[0].GetProperty("text").GetString());

        var update = message.ToAgentRunResponseUpdate();
        Assert.NotNull(update);
        Assert.Equal(ChatRole.Tool, update.Role);
        var functionResult = Assert.IsType<FunctionResultContent>(Assert.Single(update.Contents));
        var mafResult = Assert.IsType<JsonElement>(functionResult.Result);
        Assert.Equal("exploration result", mafResult[0].GetProperty("text").GetString());
    }

    [Fact]
    public void ParseMessage_ToolResultWithoutMetadata_UsesBlockContentForMaf()
    {
        const string json = """
            {
              "type": "user",
              "uuid": "user-1",
              "message": {
                "content": [
                  {
                    "type": "tool_result",
                    "tool_use_id": "tool-1",
                    "content": "fallback result"
                  }
                ]
              }
            }
            """;

        var message = Assert.IsType<UserMessage>(MessageParser.ParseMessage(json));
        var update = message.ToAgentRunResponseUpdate();

        Assert.NotNull(update);
        var functionResult = Assert.IsType<FunctionResultContent>(Assert.Single(update.Contents));
        Assert.Equal("fallback result", functionResult.Result);
    }

    [Fact]
    public void ToChatMessage_IMessageAssistant_PreservesStructuredContents()
    {
        IMessage message = new AssistantMessage
        {
            Id = "assistant-1",
            Model = "claude-test",
            SessionId = "session-1",
            Content =
            [
                new TextBlock { Text = "hello" },
                new ToolUseBlock
                {
                    Id = "tool-1",
                    Name = "read_file",
                    Input = new Dictionary<string, object> { ["path"] = "README.md" },
                },
                new ToolResultBlock
                {
                    ToolUseId = "tool-1",
                    Content = "boom",
                    ToolUseResult = new Dictionary<string, object> { ["detail"] = "boom" },
                    IsError = true,
                },
            ],
        };

        var chatMessage = Assert.IsType<ChatMessage>(message.ToChatMessage());

        Assert.Equal("claude-code", chatMessage.AuthorName);
        Assert.Equal("claude-test", chatMessage.AdditionalProperties!["modelName"]);
        Assert.Collection(
            chatMessage.Contents,
            content => Assert.Equal("hello", Assert.IsType<TextContent>(content).Text),
            content =>
            {
                var call = Assert.IsType<FunctionCallContent>(content);
                Assert.Equal("tool-1", call.CallId);
                Assert.Equal("read_file", call.Name);
            },
            content => Assert.Equal("tool-1", Assert.IsType<FunctionResultContent>(content).CallId),
            content => Assert.Equal("boom", Assert.IsType<ErrorContent>(content).Message)
        );
    }

    [Fact]
    public void ToChatMessage_IMessageSystem_ReturnsSystemMessage()
    {
        IMessage message = new SystemMessage
        {
            Id = "system-1",
            Subtype = "init",
            SessionId = "session-1",
            Data = new Dictionary<string, object> { ["status"] = "ready" },
        };

        var chatMessage = Assert.IsType<ChatMessage>(message.ToChatMessage());

        Assert.Equal(ChatRole.System, chatMessage.Role);
        Assert.Equal("claude-code", chatMessage.AuthorName);
        Assert.Contains(
            "ready",
            Assert.IsType<TextContent>(Assert.Single(chatMessage.Contents)).Text
        );
    }

    [Fact]
    public void ToChatMessage_IMessageUser_ReturnsUserMessage()
    {
        IMessage message = new UserMessage { Id = "user-1", Content = "tool output" };

        var chatMessage = Assert.IsType<ChatMessage>(message.ToChatMessage());

        Assert.Equal(ChatRole.User, chatMessage.Role);
        Assert.Equal("claude-code", chatMessage.AuthorName);
        Assert.Equal(
            "tool output",
            Assert.IsType<TextContent>(Assert.Single(chatMessage.Contents)).Text
        );
    }

    [Fact]
    public void ToChatMessage_IMessageUserToolResult_ReturnsToolMessage()
    {
        IMessage message = new UserMessage
        {
            Id = "tool-result-1",
            Content = new List<IContentBlock>
            {
                new ToolResultBlock
                {
                    ToolUseId = "tool-1",
                    Content = "boom",
                    ToolUseResult = new Dictionary<string, object> { ["detail"] = "boom" },
                    IsError = true,
                },
            },
        };

        var chatMessage = Assert.IsType<ChatMessage>(message.ToChatMessage());

        Assert.Equal(ChatRole.Tool, chatMessage.Role);
        Assert.Equal("claude-code", chatMessage.AuthorName);
        Assert.Equal(
            "tool-1",
            Assert.IsType<FunctionResultContent>(chatMessage.Contents[0]).CallId
        );
        Assert.Equal("boom", Assert.IsType<ErrorContent>(chatMessage.Contents[1]).Message);
    }

    [Fact]
    public void ToAgentRunResponseUpdate_UserMixedContent_RemainsUserMessage()
    {
        IMessage message = new UserMessage
        {
            Id = "mixed-user-1",
            Content = new List<IContentBlock>
            {
                new TextBlock { Text = "context" },
                new ToolResultBlock
                {
                    ToolUseId = "tool-1",
                    ToolUseResult = new Dictionary<string, object> { ["detail"] = "done" },
                },
            },
        };

        var update = message.ToAgentRunResponseUpdate();

        Assert.NotNull(update);
        Assert.Equal(ChatRole.User, update.Role);
        Assert.Equal("claude-code", update.AuthorName);
    }

    [Fact]
    public void ToChatMessage_IMessageResult_ReturnsFatalErrorAndUsage()
    {
        IMessage message = CreateResultMessage(
            "quota exceeded",
            new Usage { InputTokens = 10, OutputTokens = 2 }
        );

        var chatMessage = Assert.IsType<ChatMessage>(message.ToChatMessage());

        Assert.Equal(ChatRole.System, chatMessage.Role);
        Assert.Equal("claude-code", chatMessage.AuthorName);
        Assert.Collection(
            chatMessage.Contents,
            content =>
            {
                var error = Assert.IsType<ErrorContent>(content);
                Assert.Equal("quota exceeded", error.Message);
                Assert.True(Assert.IsType<bool>(error.AdditionalProperties!["isFatalError"]));
            },
            content => Assert.IsType<UsageContent>(content)
        );
    }

    [Fact]
    public void ToChatMessage_IMessageSuccessfulResult_ReturnsTextAndUsage()
    {
        IMessage message = CreateResultMessage(
            "done",
            new Usage { InputTokens = 10, OutputTokens = 2 }
        ) with
        {
            IsError = false,
            Subtype = "success",
        };

        var chatMessage = Assert.IsType<ChatMessage>(message.ToChatMessage());

        Assert.Equal(ChatRole.System, chatMessage.Role);
        Assert.Collection(
            chatMessage.Contents,
            content => Assert.Equal("done", Assert.IsType<TextContent>(content).Text),
            content => Assert.IsType<UsageContent>(content)
        );
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
