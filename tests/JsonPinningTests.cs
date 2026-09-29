using System.Text;
using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Xunit;

namespace ClaudeCodeSdk.Tests;

/// <summary>
/// Pins the exact JSON the SDK writes for stdin messages, control responses, and
/// --mcp-config, so the AOT-compatible JSON rewrite cannot change wire output.
/// </summary>
public class JsonPinningTests
{
    [Fact]
    public async Task WriteJsonLineToAsync_UserMessage_WritesExpectedJson()
    {
        var message = new Dictionary<string, object>
        {
            ["type"] = "user",
            ["message"] = new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = "hello",
            },
            ["parent_tool_use_id"] = null!,
            ["session_id"] = "default",
        };

        using var stream = new MemoryStream();
        await ClaudeProcess.WriteJsonLineToAsync(
            stream,
            message,
            TestContext.Current.CancellationToken
        );

        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal(
            "{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":\"hello\"},\"parent_tool_use_id\":null,\"session_id\":\"default\"}\n",
            json
        );
    }

    [Fact]
    public async Task TryHandle_CanUseToolDeny_WritesExactJson()
    {
        var writtenLine = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var handler = new ControlProtocolHandler(
            (_, _, _, _) =>
                ValueTask.FromResult<PermissionResult>(
                    new PermissionResultDeny("nope", Interrupt: true)
                ),
            (line, _) =>
            {
                writtenLine.TrySetResult(line);
                return Task.CompletedTask;
            }
        );

        handler.TryHandle(CreateRequest("req-1"), TestContext.Current.CancellationToken);
        var line = await writtenLine.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            "{\"type\":\"control_response\",\"response\":{\"subtype\":\"success\",\"request_id\":\"req-1\",\"response\":{\"behavior\":\"deny\",\"message\":\"nope\",\"interrupt\":true}}}",
            line
        );
    }

    [Fact]
    public void BuildCommand_McpHttpServerConfig_WritesExpectedJson()
    {
        var options = new ClaudeCodeOptions
        {
            McpServers = new Dictionary<string, IMcpServerConfig>
            {
                ["srv"] = new McpHttpServerConfig
                {
                    Url = "https://example.com",
                    Headers = new Dictionary<string, string> { ["X-Test"] = "1" },
                },
            },
        };

        var json = GetMcpConfigArgument(options);
        // The dictionary value is declared as IMcpServerConfig, so System.Text.Json
        // serializes only the interface's own member ("type"); this is existing
        // behavior that this test pins, not a bug introduced by the AOT rewrite.
        Assert.Equal("{\"srv\":{\"type\":\"http\"}}", json);
    }

    [Fact]
    public void BuildCommand_McpSSEServerConfig_WritesExpectedJson()
    {
        var options = new ClaudeCodeOptions
        {
            McpServers = new Dictionary<string, IMcpServerConfig>
            {
                ["srv"] = new McpSSEServerConfig
                {
                    Url = "https://example.com/sse",
                    Headers = new Dictionary<string, string> { ["X-Test"] = "1" },
                },
            },
        };

        var json = GetMcpConfigArgument(options);
        Assert.Equal("{\"srv\":{\"type\":\"sse\"}}", json);
    }

    [Fact]
    public void BuildCommand_McpStdioServerConfig_WritesExpectedJson()
    {
        var options = new ClaudeCodeOptions
        {
            McpServers = new Dictionary<string, IMcpServerConfig>
            {
                ["srv"] = new McpStdioServerConfig
                {
                    Command = "node",
                    Args = new[] { "server.js" },
                    Environment = new Dictionary<string, string> { ["KEY"] = "value" },
                },
            },
        };

        var json = GetMcpConfigArgument(options);
        Assert.Equal("{\"srv\":{\"type\":\"stdio\"}}", json);
    }

    private static string GetMcpConfigArgument(ClaudeCodeOptions options)
    {
        var command = CommandUtil.BuildCommand(options, isStreaming: true, prompt: string.Empty);
        var index = command.IndexOf("--mcp-config");
        Assert.True(index >= 0);
        return command[index + 1];
    }

    private static string CreateRequest(string requestId) =>
        $$"""
            {
              "type": "control_request",
              "request_id": "{{requestId}}",
              "request": {
                "subtype": "can_use_tool",
                "tool_name": "SomeTool",
                "tool_use_id": "call-1",
                "input": { "a": 1 }
              }
            }
            """;
}
