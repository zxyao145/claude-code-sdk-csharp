using ClaudeCodeSdk.Types;
using Xunit;

namespace ClaudeCodeSdk.Tests;

public class TypesTests
{
    [Fact]
    public void TextBlock_ShouldHaveCorrectType()
    {
        var block = new TextBlock { Text = "Hello, World!" };
        Assert.Equal("text", block.Type);
        Assert.Equal("Hello, World!", block.Text);
    }

    [Fact]
    public void ThinkingBlock_ShouldHaveCorrectType()
    {
        var block = new ThinkingBlock
        {
            Thinking = "I need to think about this",
            Signature = "sig123"
        };

        Assert.Equal("thinking", block.Type);
        Assert.Equal("I need to think about this", block.Thinking);
        Assert.Equal("sig123", block.Signature);
    }

    [Fact]
    public void ToolUseBlock_ShouldHaveCorrectType()
    {
        var input = new Dictionary<string, object> { ["param"] = "value" };
        var block = new ToolUseBlock
        {
            Id = "tool1",
            Name = "TestTool",
            Input = input
        };

        Assert.Equal("tool_use", block.Type);
        Assert.Equal("tool1", block.Id);
        Assert.Equal("TestTool", block.Name);
        Assert.Equal(input, block.Input);
    }

    [Fact]
    public void ToolResultBlock_ShouldHaveCorrectType()
    {
        var block = new ToolResultBlock
        {
            ToolUseId = "tool1",
            Content = "Result content",
            IsError = false
        };

        Assert.Equal("tool_result", block.Type);
        Assert.Equal("tool1", block.ToolUseId);
        Assert.Equal("Result content", block.Content);
        Assert.False(block.IsError);
    }

    [Fact]
    public void UserMessage_ShouldHaveCorrectType()
    {
        var message = new UserMessage { Content = "Hello" };
        Assert.Equal("user", message.Type.Value);
        Assert.Equal("Hello", message.Content);
    }

    [Fact]
    public void AssistantMessage_ShouldHaveCorrectType()
    {
        var content = new List<IContentBlock>
        {
            new TextBlock { Text = "Hello" }
        };

        var message = new AssistantMessage
        {
            Content = content,
            Model = "claude-3",
            SessionId = "123"
        };

        Assert.Equal("assistant", message.Type.Value);
        Assert.Equal(content, message.Content);
        Assert.Equal("claude-3", message.Model);
        Assert.Equal("123", message.SessionId);
    }

    [Fact]
    public void SystemMessage_ShouldHaveCorrectType()
    {
        var data = new Dictionary<string, object> { ["key"] = "value" };
        var message = new SystemMessage
        {
            Subtype = "test",
            Data = data,
            SessionId = "123"
        };

        Assert.Equal("system", message.Type.Value);
        Assert.Equal("test", message.Subtype);
        Assert.Equal(data, message.Data);
        Assert.Equal("123", message.SessionId);
    }

    [Fact]
    public void ResultMessage_ShouldHaveCorrectType()
    {
        var message = new ResultMessage
        {
            Subtype = "completion",
            DurationMs = 1000,
            DurationApiMs = 800,
            IsError = false,
            NumTurns = 1,
            SessionId = "session123",
            TotalCostUsd = 0.001
        };

        Assert.Equal("result", message.Type.Value);
        Assert.Equal("completion", message.Subtype);
        Assert.Equal(1000, message.DurationMs);
        Assert.Equal(800, message.DurationApiMs);
        Assert.False(message.IsError);
        Assert.Equal(1, message.NumTurns);
        Assert.Equal("session123", message.SessionId);
        Assert.Equal(0.001, message.TotalCostUsd);
    }

    [Fact]
    public void ClaudeCodeOptions_ShouldHaveDefaults()
    {
        var options = new ClaudeCodeOptions();

        Assert.Empty(options.AllowedTools);
        Assert.Equal(8000, options.MaxThinkingTokens);
        Assert.Null(options.SystemPrompt);
        Assert.Null(options.AppendSystemPrompt);
        Assert.Empty(options.McpServers);
        Assert.Null(options.PermissionMode);
        Assert.False(options.ContinueConversation);
        Assert.Null(options.Resume);
        Assert.Null(options.MaxTurns);
        Assert.Empty(options.DisallowedTools);
        Assert.Null(options.Model);
        Assert.Null(options.WorkingDirectory);
        Assert.Empty(options.AddDirectories);
        Assert.Empty(options.ExtraArgs);
    }
}