using System.Text.Json;
using ClaudeCodeSdk.Exceptions;
using ClaudeCodeSdk.Types;
using Xunit;

namespace ClaudeCodeSdk.Tests;

/// <summary>
/// Each stdout line is parsed once and shared by the control protocol and the message parser.
/// </summary>
public class StdoutLineParsingTests
{
    private const string ResultLine = """
        {"type":"result","subtype":"success","is_error":false,"duration_ms":1,"duration_api_ms":1,"num_turns":1,"session_id":"session","uuid":"result-1","result":"done"}
        """;

    [Fact]
    public async Task ReadResponseAsync_NullLine_IsSkipped()
    {
        // Arrange
        using var reader = new StringReader(string.Join('\n', "null", ResultLine));

        // Act
        var messages = await BackgroundAgentCompletionTests.ReadAsync(
            reader,
            TestContext.Current.CancellationToken
        );

        // Assert
        var result = Assert.IsType<ResultMessage>(Assert.Single(messages));
        Assert.Equal("done", result.Result);
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    [InlineData("{not json")]
    public async Task ReadResponseAsync_NonObjectOrInvalidLine_ThrowsCLIJsonDecodeException(
        string line
    )
    {
        // Arrange
        using var reader = new StringReader(string.Join('\n', line, ResultLine));

        // Act
        var exception = await Assert.ThrowsAsync<CLIJsonDecodeException>(() =>
            BackgroundAgentCompletionTests.ReadAsync(reader, TestContext.Current.CancellationToken)
        );

        // Assert
        Assert.Equal(line, exception.Line);
    }

    [Fact]
    public async Task ReadResponseAsync_ControlResponse_IsConsumedWithoutYielding()
    {
        // Arrange
        using var reader = new StringReader(
            string.Join(
                '\n',
                """{"type":"control_response","response":{"subtype":"success","request_id":"r1"}}""",
                ResultLine
            )
        );

        // Act
        var messages = await BackgroundAgentCompletionTests.ReadAsync(
            reader,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.IsType<ResultMessage>(Assert.Single(messages));
    }

    [Theory]
    [InlineData("[1]")]
    [InlineData("null")]
    [InlineData("{\"type\":null}")]
    [InlineData("{\"type\":\"assistant\"}")]
    public void TryHandle_NonControlLine_ReturnsFalse(string line)
    {
        // Arrange
        var handler = new ControlProtocolHandler(null, (_, _) => Task.CompletedTask);

        // Act
        var handled = handler.TryHandle(line, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(handled);
    }

    [Fact]
    public void ParseMessage_ToolResultNullContent_IsNull()
    {
        // Arrange
        var json = ToolResultJson(content: "null", toolUseResult: "null");

        // Act
        var toolResult = ParseToolResult(json);

        // Assert
        Assert.Null(toolResult.Content);
        Assert.Null(toolResult.ToolUseResult);
    }

    [Theory]
    [InlineData("[1]")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("{\"stdout\":\"ok\"}")]
    public void ParseMessage_ToolResultJsonContent_IsJsonElement(string value)
    {
        // Arrange
        var json = ToolResultJson(content: value, toolUseResult: value);

        // Act
        var toolResult = ParseToolResult(json);

        // Assert
        var expected = JsonElement.Parse(value);
        Assert.True(
            JsonElement.DeepEquals(expected, Assert.IsType<JsonElement>(toolResult.Content))
        );
        Assert.True(
            JsonElement.DeepEquals(expected, Assert.IsType<JsonElement>(toolResult.ToolUseResult))
        );
    }

    private static ToolResultBlock ParseToolResult(string json)
    {
        var message = Assert.IsType<UserMessage>(MessageParser.ParseMessage(json));
        var contentBlocks = Assert.IsType<List<IContentBlock>>(message.Content);
        return Assert.IsType<ToolResultBlock>(Assert.Single(contentBlocks));
    }

    private static string ToolResultJson(string content, string toolUseResult) =>
        """{"type":"user","uuid":"user-1","message":{"content":[{"type":"tool_result","tool_use_id":"tool-1","content":"""
        + content
        + """}]},"tool_use_result":"""
        + toolUseResult
        + "}";
}
