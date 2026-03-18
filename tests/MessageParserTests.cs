using ClaudeCodeSdk.Types;
using Xunit;

namespace ClaudeCodeSdk.Tests;

public class MessageParserTests
{
    [Fact]
    public void ParseMessage_ShouldDeserializeAssistantMessageViaPolymorphism()
    {
        var json = """
        {
          "type": "assistant",
          "uuid": "assistant-1",
          "session_id": "session-1",
          "message": {
            "role": "assistant",
            "model": "claude-sonnet-4",
            "content": [
              {
                "type": "text",
                "text": "Hello from Claude"
              }
            ]
          }
        }
        """;

        var message = MessageParser.ParseMessage(json);

        var assistant = Assert.IsType<AssistantMessage>(message);
        Assert.Equal("assistant-1", assistant.Id);
        Assert.Equal("session-1", assistant.SessionId);
        Assert.Equal("claude-sonnet-4", assistant.Model);

        var textBlock = Assert.IsType<TextBlock>(Assert.Single(assistant.Content));
        Assert.Equal("Hello from Claude", textBlock.Text);
    }

    [Fact]
    public void ParseMessage_ShouldDeserializeUserMessageStringContent()
    {
        var json = """
        {
          "type": "user",
          "uuid": "user-1",
          "message": {
            "role": "user",
            "content": "Explain the change"
          }
        }
        """;

        var message = MessageParser.ParseMessage(json);

        var user = Assert.IsType<UserMessage>(message);
        Assert.Equal("user-1", user.Id);
        Assert.Equal("Explain the change", Assert.IsType<string>(user.Content));
    }

    [Fact]
    public void ParseMessage_ShouldDeserializeUserMessageBlockContent()
    {
        var json = """
        {
          "type": "user",
          "uuid": "user-2",
          "message": {
            "role": "user",
            "content": [
              {
                "type": "tool_result",
                "tool_use_id": "tool-1",
                "content": "done",
                "tool_use_result": {
                  "status": "ok"
                },
                "is_error": false
              }
            ]
          }
        }
        """;

        var message = MessageParser.ParseMessage(json);

        var user = Assert.IsType<UserMessage>(message);
        var contentBlocks = Assert.IsAssignableFrom<IReadOnlyList<IContentBlock>>(user.Content);
        var toolResult = Assert.IsType<ToolResultBlock>(Assert.Single(contentBlocks));
        Assert.Equal("tool-1", toolResult.ToolUseId);
        Assert.Equal(false, toolResult.IsError);
    }

    [Fact]
    public void ParseMessage_ShouldDeserializeSystemMessageData()
    {
        var json = """
        {
          "type": "system",
          "uuid": "system-1",
          "session_id": "session-1",
          "subtype": "init",
          "cwd": "/workspace/repo"
        }
        """;

        var message = MessageParser.ParseMessage(json);

        var system = Assert.IsType<SystemMessage>(message);
        Assert.Equal("system-1", system.Id);
        Assert.Equal("init", system.Subtype);
        Assert.Equal("session-1", system.SessionId);
        Assert.True(system.Data.ContainsKey("session_id"));
        Assert.DoesNotContain("subtype", system.Data.Keys);
    }

    [Fact]
    public void ParseMessage_ShouldConvertAssistantErrorIntoErrorContentBlock()
    {
        var json = """
        {
          "type": "assistant",
          "uuid": "assistant-2",
          "session_id": "session-2",
          "error": "tool failed",
          "message": {
            "role": "assistant",
            "model": "claude-sonnet-4",
            "content": []
          }
        }
        """;

        var message = MessageParser.ParseMessage(json);

        var assistant = Assert.IsType<AssistantMessage>(message);
        var errorBlock = Assert.IsType<ErrorContentBlock>(Assert.Single(assistant.Content));
        Assert.Equal("tool failed", errorBlock.Message);
    }
}
