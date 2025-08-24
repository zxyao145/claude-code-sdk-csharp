using Xunit;
using ClaudeCodeSdk.Exceptions;

namespace ClaudeCodeSdk.Tests;

public class ExceptionsTests
{
    [Fact]
    public void ClaudeSDKException_ShouldBeBaseException()
    {
        var ex = new ClaudeSDKException("Test message");
        Assert.Equal("Test message", ex.Message);
        Assert.IsAssignableFrom<Exception>(ex);
    }

    [Fact]
    public void CLIConnectionException_ShouldInheritFromClaudeSDKException()
    {
        var ex = new CLIConnectionException("Connection failed");
        Assert.Equal("Connection failed", ex.Message);
        Assert.IsAssignableFrom<ClaudeSDKException>(ex);
    }

    [Fact]
    public void CLINotFoundException_ShouldIncludeCliPathInMessage()
    {
        var ex = new CLINotFoundException("Claude Code not found", "/path/to/claude");
        Assert.Contains("/path/to/claude", ex.Message);
        Assert.Equal("/path/to/claude", ex.CliPath);
    }

    [Fact]
    public void ProcessException_ShouldIncludeExitCodeInMessage()
    {
        var ex = new ProcessException("Process failed", 1, "stderr output");
        Assert.Contains("exit code: 1", ex.Message);
        Assert.Contains("stderr output", ex.Message);
        Assert.Equal(1, ex.ExitCode);
        Assert.Equal("stderr output", ex.StandardError);
    }

    [Fact]
    public void CLIJsonDecodeException_ShouldTruncateLongLines()
    {
        var longLine = new string('a', 150);
        var originalError = new Exception("JSON error");
        var ex = new CLIJsonDecodeException(longLine, originalError);
        
        Assert.Contains("...", ex.Message);
        Assert.Equal(longLine, ex.Line);
        Assert.Equal(originalError, ex.OriginalError);
    }

    [Fact]
    public void MessageParseException_ShouldStoreData()
    {
        var data = new { type = "test", content = "data" };
        var ex = new MessageParseException("Parse failed", data);
        
        Assert.Equal("Parse failed", ex.Message);
        Assert.Equal(data, ex.ExpData);
    }
}