using System.Text;
using ClaudeCodeSdk;
using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Microsoft.Extensions.AI;
using Xunit;

namespace ClaudeCodeSdk.ReflectionFreeTests;

/// <summary>
/// Exercises the SDK's JSON paths with JsonSerializerIsReflectionEnabledByDefault=false
/// (set in the csproj), so a call site that still needs reflection fails here instead of
/// only at a native AOT publish, which this environment cannot run.
/// </summary>
public class ReflectionFreeTests
{
    [Fact]
    public async Task WriteJsonLineToAsync_UserMessage_Succeeds()
    {
        var message = new Dictionary<string, object>
        {
            ["type"] = "user",
            ["message"] = new Dictionary<string, object> { ["role"] = "user", ["content"] = "hi" },
            ["session_id"] = "default",
        };

        using var stream = new MemoryStream();
        await ClaudeProcess.WriteJsonLineToAsync(
            stream,
            message,
            TestContext.Current.CancellationToken
        );

        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("\"type\":\"user\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ControlProtocolHandler_CanUseToolDeny_WritesResponse()
    {
        var writtenLine = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var handler = new ControlProtocolHandler(
            (_, _, _, _) => ValueTask.FromResult<PermissionResult>(new PermissionResultDeny("no")),
            (line, _) =>
            {
                writtenLine.TrySetResult(line);
                return Task.CompletedTask;
            }
        );

        handler.TryHandle(
            """
            {
              "type": "control_request",
              "request_id": "req-1",
              "request": {
                "subtype": "can_use_tool",
                "tool_name": "Bash",
                "tool_use_id": "call-1",
                "input": {}
              }
            }
            """,
            TestContext.Current.CancellationToken
        );
        var line = await writtenLine.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );

        Assert.Contains("\"behavior\":\"deny\"", line, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(McpServerConfigs))]
    public void BuildCommand_McpConfig_WritesExpectedJson(
        IMcpServerConfig config,
        string expectedJson
    )
    {
        var options = new ClaudeCodeOptions
        {
            McpServers = new Dictionary<string, IMcpServerConfig> { ["srv"] = config },
        };

        var command = CommandUtil.BuildCommand(options, isStreaming: true, prompt: string.Empty);

        var index = command.IndexOf("--mcp-config");
        Assert.True(index >= 0);
        Assert.Equal(expectedJson, command[index + 1]);
    }

    public static IEnumerable<object[]> McpServerConfigs()
    {
        yield return
        [
            new McpHttpServerConfig
            {
                Url = "https://example.com",
                Headers = new Dictionary<string, string> { ["X-Test"] = "1" },
            },
            "{\"mcpServers\":{\"srv\":{\"type\":\"http\",\"url\":\"https://example.com\",\"headers\":{\"X-Test\":\"1\"}}}}",
        ];
        yield return
        [
            new McpSSEServerConfig
            {
                Url = "https://example.com/sse",
                Headers = new Dictionary<string, string> { ["X-Test"] = "1" },
            },
            "{\"mcpServers\":{\"srv\":{\"type\":\"sse\",\"url\":\"https://example.com/sse\",\"headers\":{\"X-Test\":\"1\"}}}}",
        ];
        yield return
        [
            new McpStdioServerConfig
            {
                Command = "node",
                Args = new[] { "server.js" },
                Environment = new Dictionary<string, string> { ["KEY"] = "value" },
            },
            "{\"mcpServers\":{\"srv\":{\"type\":\"stdio\",\"command\":\"node\",\"args\":[\"server.js\"],\"env\":{\"KEY\":\"value\"}}}}",
        ];
        yield return
        [
            new McpHttpServerConfig { Url = "https://example.com" },
            "{\"mcpServers\":{\"srv\":{\"type\":\"http\",\"url\":\"https://example.com\"}}}",
        ];
        yield return
        [
            new McpSSEServerConfig { Url = "https://example.com/sse" },
            "{\"mcpServers\":{\"srv\":{\"type\":\"sse\",\"url\":\"https://example.com/sse\"}}}",
        ];
        yield return
        [
            new McpStdioServerConfig { Command = "node" },
            "{\"mcpServers\":{\"srv\":{\"type\":\"stdio\",\"command\":\"node\"}}}",
        ];
    }

    [Fact]
    public void MessageParser_SystemInitWithData_Parses()
    {
        var message = MessageParser.ParseMessage(
            """
            {
              "type": "system",
              "subtype": "init",
              "uuid": "u1",
              "session_id": "s1",
              "cwd": "/tmp",
              "tools": ["Bash"]
            }
            """
        );

        var system = Assert.IsType<SystemMessage>(message);
        Assert.Equal("init", system.Subtype);
        Assert.True(system.Data.ContainsKey("cwd"));
    }

    [Fact]
    public void MessageParser_AssistantWithToolUseInput_Parses()
    {
        var message = MessageParser.ParseMessage(
            """
            {
              "type": "assistant",
              "uuid": "u1",
              "session_id": "s1",
              "message": {
                "id": "m1",
                "model": "claude",
                "content": [
                  {
                    "type": "tool_use",
                    "id": "call-1",
                    "name": "Bash",
                    "input": { "command": "pwd" }
                  }
                ]
              }
            }
            """
        );

        var assistant = Assert.IsType<AssistantMessage>(message);
        var toolUse = Assert.IsType<ToolUseBlock>(Assert.Single(assistant.Content));
        Assert.Equal("Bash", toolUse.Name);
        Assert.True(toolUse.Input.ContainsKey("command"));
    }

    [Fact]
    public void MessageParser_ResultWithUsage_Parses()
    {
        var message = MessageParser.ParseMessage(
            """
            {
              "type": "result",
              "uuid": "u1",
              "subtype": "success",
              "duration_ms": 1,
              "duration_api_ms": 1,
              "is_error": false,
              "num_turns": 1,
              "session_id": "s1",
              "usage": { "input_tokens": 1, "output_tokens": 2 }
            }
            """
        );

        var result = Assert.IsType<ResultMessage>(message);
        Assert.NotNull(result.Usage);
        Assert.Equal(1, result.Usage!.InputTokens);
        Assert.Equal(2, result.Usage!.OutputTokens);
    }

    [Fact]
    public void ControlProtocolHandler_TryHandle_ControlRequest_Succeeds()
    {
        var handler = new ControlProtocolHandler(
            (_, _, _, _) => ValueTask.FromResult<PermissionResult>(new PermissionResultAllow()),
            (_, _) => Task.CompletedTask
        );

        var handled = handler.TryHandle(
            """
            {
              "type": "control_request",
              "request_id": "req-2",
              "request": {
                "subtype": "can_use_tool",
                "tool_name": "Bash",
                "tool_use_id": "call-2",
                "input": {}
              }
            }
            """,
            TestContext.Current.CancellationToken
        );

        Assert.True(handled);
    }

    [Fact]
    public void ClaudePartialMessageMapper_ToolInput_Deserializes()
    {
        var mapper = new ClaudePartialMessageMapper();
        var events = new[]
        {
            ParseStreamEvent(
                """
                {"type":"stream_event","uuid":"e1","session_id":"s1","parent_tool_use_id":null,"event":{"type":"message_start","message":{"id":"m1","model":"claude"}}}
                """
            ),
            ParseStreamEvent(
                """
                {"type":"stream_event","uuid":"e2","session_id":"s1","parent_tool_use_id":null,"event":{"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"call-1","name":"Bash","input":{}}}}
                """
            ),
            ParseStreamEvent(
                """
                {"type":"stream_event","uuid":"e3","session_id":"s1","parent_tool_use_id":null,"event":{"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"command\":\"pwd\"}"}}}
                """
            ),
            ParseStreamEvent(
                """
                {"type":"stream_event","uuid":"e4","session_id":"s1","parent_tool_use_id":null,"event":{"type":"content_block_stop","index":0}}
                """
            ),
            ParseStreamEvent(
                """
                {"type":"stream_event","uuid":"e5","session_id":"s1","parent_tool_use_id":null,"event":{"type":"message_delta","delta":{"stop_reason":"tool_use"}}}
                """
            ),
            ParseStreamEvent(
                """
                {"type":"stream_event","uuid":"e6","session_id":"s1","parent_tool_use_id":null,"event":{"type":"message_stop"}}
                """
            ),
        };

        var functionCall = events
            .SelectMany(mapper.Map)
            .SelectMany(update => update.Contents)
            .OfType<FunctionCallContent>()
            .Single();

        Assert.Equal("call-1", functionCall.CallId);
        Assert.Equal("Bash", functionCall.Name);
        Assert.Equal("pwd", functionCall.Arguments!["command"]?.ToString());
    }

    private static StreamEvent ParseStreamEvent(string json) =>
        Assert.IsType<StreamEvent>(MessageParser.ParseMessage(json));

    [Fact]
    public async Task AgentSession_SerializeDeserialize_RoundTrips()
    {
        await using var agent = new ClaudeCodeAIAgent();
        var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);

        var serialized = await agent.SerializeSessionAsync(
            session,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var restored = await agent.DeserializeSessionAsync(
            serialized,
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.NotNull(restored);
    }
}
