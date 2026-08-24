using System.Text.Json;
using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Xunit;

namespace ClaudeCodeSdk.Tests;

public class ControlProtocolTests
{
    [Fact]
    public void BuildCommand_WithCanUseTool_UsesStdioPermissionPromptTool()
    {
        // Arrange
        var options = new ClaudeCodeOptions { CanUseTool = AllowOriginalInput };

        // Act
        var command = CommandUtil.BuildCommand(options, isStreaming: true, prompt: string.Empty);

        // Assert
        var optionIndex = command.IndexOf("--permission-prompt-tool");
        Assert.True(optionIndex >= 0);
        Assert.Equal("stdio", command[optionIndex + 1]);
    }

    [Fact]
    public void BuildCommand_WithCanUseToolAndDifferentPromptTool_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new ClaudeCodeOptions
        {
            CanUseTool = AllowOriginalInput,
            PermissionPromptToolName = "mcp__permissions__check",
        };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CommandUtil.BuildCommand(options, isStreaming: true, prompt: string.Empty)
        );

        // Assert
        Assert.Contains("stdio", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandle_CanUseToolAllow_WritesUpdatedInputResponse()
    {
        // Arrange
        var writtenLine = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updatedInput = JsonSerializer.SerializeToElement(
            new
            {
                questions = new[] { new { question = "Choose?" } },
                answers = new Dictionary<string, string> { ["Choose?"] = "A" },
            }
        );
        var handler = new ControlProtocolHandler(
            (toolName, input, context, cancellationToken) =>
            {
                Assert.Equal("AskUserQuestion", toolName);
                Assert.Equal("call-1", context.ToolUseId);
                Assert.True(input.TryGetProperty("questions", out _));
                Assert.False(cancellationToken.IsCancellationRequested);
                return ValueTask.FromResult<PermissionResult>(new PermissionResultAllow(updatedInput));
            },
            (line, _) =>
            {
                writtenLine.TrySetResult(line);
                return Task.CompletedTask;
            }
        );

        // Act
        var handled = handler.TryHandle(CreateRequest("request-1"), TestContext.Current.CancellationToken);
        var responseLine = await writtenLine.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(handled);
        using var response = JsonDocument.Parse(responseLine);
        var responseValue = response.RootElement.GetProperty("response");
        Assert.Equal("success", responseValue.GetProperty("subtype").GetString());
        Assert.Equal("request-1", responseValue.GetProperty("request_id").GetString());
        var permission = responseValue.GetProperty("response");
        Assert.Equal("allow", permission.GetProperty("behavior").GetString());
        Assert.Equal(
            "A",
            permission.GetProperty("updatedInput").GetProperty("answers").GetProperty("Choose?").GetString()
        );
    }

    [Fact]
    public async Task TryHandle_CanUseToolDeny_WritesDenyResponse()
    {
        // Arrange
        var writtenLine = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ControlProtocolHandler(
            (_, _, _, _) =>
                ValueTask.FromResult<PermissionResult>(new PermissionResultDeny("User cancelled.", Interrupt: false)),
            (line, _) =>
            {
                writtenLine.TrySetResult(line);
                return Task.CompletedTask;
            }
        );

        // Act
        handler.TryHandle(CreateRequest("request-2"), TestContext.Current.CancellationToken);
        var responseLine = await writtenLine.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );

        // Assert
        using var response = JsonDocument.Parse(responseLine);
        var permission = response.RootElement.GetProperty("response").GetProperty("response");
        Assert.Equal("deny", permission.GetProperty("behavior").GetString());
        Assert.Equal("User cancelled.", permission.GetProperty("message").GetString());
        Assert.False(permission.GetProperty("interrupt").GetBoolean());
    }

    [Fact]
    public async Task TryHandle_CanUseToolAllowWithoutUpdatedInput_PreservesOriginalInput()
    {
        // Arrange
        var writtenLine = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ControlProtocolHandler(
            (_, _, _, _) => ValueTask.FromResult<PermissionResult>(new PermissionResultAllow()),
            (line, _) =>
            {
                writtenLine.TrySetResult(line);
                return Task.CompletedTask;
            }
        );

        // Act
        handler.TryHandle(CreateRequest("request-original"), TestContext.Current.CancellationToken);
        var responseLine = await writtenLine.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );

        // Assert
        using var response = JsonDocument.Parse(responseLine);
        var input = response
            .RootElement.GetProperty("response")
            .GetProperty("response")
            .GetProperty("updatedInput");
        Assert.Equal("Choose?", input.GetProperty("questions")[0].GetProperty("question").GetString());
    }

    [Fact]
    public async Task TryHandle_ControlCancelRequest_CancelsPendingCallbackWithoutWritingResponse()
    {
        // Arrange
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ControlProtocolHandler(
            async (_, _, _, cancellationToken) =>
            {
                callbackStarted.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return new PermissionResultAllow();
                }
                finally
                {
                    cancellationObserved.TrySetResult();
                }
            },
            (_, _) =>
            {
                writeAttempted.TrySetResult();
                return Task.CompletedTask;
            }
        );

        // Act
        handler.TryHandle(CreateRequest("request-3"), TestContext.Current.CancellationToken);
        await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var handled = handler.TryHandle(
            """{"type":"control_cancel_request","request_id":"request-3"}""",
            TestContext.Current.CancellationToken
        );
        await cancellationObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(handled);
        Assert.False(writeAttempted.Task.IsCompleted);
    }

    [Fact]
    public async Task TryHandle_CallbackFailure_WritesErrorResponse()
    {
        // Arrange
        var writtenLine = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ControlProtocolHandler(
            (_, _, _, _) => throw new InvalidOperationException("Callback failed."),
            (line, _) =>
            {
                writtenLine.TrySetResult(line);
                return Task.CompletedTask;
            }
        );

        // Act
        handler.TryHandle(CreateRequest("request-4"), TestContext.Current.CancellationToken);
        var responseLine = await writtenLine.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );

        // Assert
        using var response = JsonDocument.Parse(responseLine);
        var responseValue = response.RootElement.GetProperty("response");
        Assert.Equal("error", responseValue.GetProperty("subtype").GetString());
        Assert.Equal("Callback failed.", responseValue.GetProperty("error").GetString());
    }

    [Fact]
    public void ClaudeCodeOptions_CanUseTool_IsIgnoredByJsonAndPreservedByMafMapping()
    {
        // Arrange
        var options = new ClaudeCodeAIAgentOptions { CanUseTool = AllowOriginalInput };

        // Act
        var json = JsonSerializer.Serialize(options);
        var mapped = options.ToClaudeCodeOptions();
        var roundTripped = ClaudeCodeAIAgentOptions.From(mapped);

        // Assert
        Assert.DoesNotContain("CanUseTool", json, StringComparison.Ordinal);
        Assert.Same(options.CanUseTool, mapped.CanUseTool);
        Assert.Same(options.CanUseTool, roundTripped!.CanUseTool);
    }

    private static ValueTask<PermissionResult> AllowOriginalInput(
        string _,
        JsonElement input,
        ToolPermissionContext __,
        CancellationToken ___
    ) => ValueTask.FromResult<PermissionResult>(new PermissionResultAllow(input));

    private static string CreateRequest(string requestId) =>
        $$"""
        {
          "type": "control_request",
          "request_id": "{{requestId}}",
          "request": {
            "subtype": "can_use_tool",
            "tool_name": "AskUserQuestion",
            "tool_use_id": "call-1",
            "input": {
              "questions": [
                {
                  "question": "Choose?",
                  "header": "Choice",
                  "options": [
                    { "label": "A", "description": "First" },
                    { "label": "B", "description": "Second" }
                  ],
                  "multiSelect": false
                }
              ]
            }
          }
        }
        """;
}
