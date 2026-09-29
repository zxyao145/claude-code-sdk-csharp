using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Xunit;

namespace ClaudeCodeSdk.Tests;

/// <summary>
/// Verifies the exact --mcp-config JSON that CommandUtil.BuildCommand produces for each
/// MCP server config type, so the CLI receives full configs under an "mcpServers" wrapper.
/// </summary>
public class McpConfigTests
{
    [Fact]
    public void BuildCommand_StdioServerWithAllFields_WritesFullConfig()
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

        Assert.Equal(
            "{\"mcpServers\":{\"srv\":{\"type\":\"stdio\",\"command\":\"node\",\"args\":[\"server.js\"],\"env\":{\"KEY\":\"value\"}}}}",
            GetMcpConfigArgument(options)
        );
    }

    [Fact]
    public void BuildCommand_StdioServerWithOptionalFieldsNull_OmitsThem()
    {
        var options = new ClaudeCodeOptions
        {
            McpServers = new Dictionary<string, IMcpServerConfig>
            {
                ["srv"] = new McpStdioServerConfig { Command = "true" },
            },
        };

        Assert.Equal(
            "{\"mcpServers\":{\"srv\":{\"type\":\"stdio\",\"command\":\"true\"}}}",
            GetMcpConfigArgument(options)
        );
    }

    [Fact]
    public void BuildCommand_SSEServerWithAllFields_WritesFullConfig()
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

        Assert.Equal(
            "{\"mcpServers\":{\"srv\":{\"type\":\"sse\",\"url\":\"https://example.com/sse\",\"headers\":{\"X-Test\":\"1\"}}}}",
            GetMcpConfigArgument(options)
        );
    }

    [Fact]
    public void BuildCommand_SSEServerWithOptionalFieldsNull_OmitsThem()
    {
        var options = new ClaudeCodeOptions
        {
            McpServers = new Dictionary<string, IMcpServerConfig>
            {
                ["srv"] = new McpSSEServerConfig { Url = "https://example.com/sse" },
            },
        };

        Assert.Equal(
            "{\"mcpServers\":{\"srv\":{\"type\":\"sse\",\"url\":\"https://example.com/sse\"}}}",
            GetMcpConfigArgument(options)
        );
    }

    [Fact]
    public void BuildCommand_HttpServerWithAllFields_WritesFullConfig()
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

        Assert.Equal(
            "{\"mcpServers\":{\"srv\":{\"type\":\"http\",\"url\":\"https://example.com\",\"headers\":{\"X-Test\":\"1\"}}}}",
            GetMcpConfigArgument(options)
        );
    }

    [Fact]
    public void BuildCommand_HttpServerWithOptionalFieldsNull_OmitsThem()
    {
        var options = new ClaudeCodeOptions
        {
            McpServers = new Dictionary<string, IMcpServerConfig>
            {
                ["srv"] = new McpHttpServerConfig { Url = "https://example.com" },
            },
        };

        Assert.Equal(
            "{\"mcpServers\":{\"srv\":{\"type\":\"http\",\"url\":\"https://example.com\"}}}",
            GetMcpConfigArgument(options)
        );
    }

    [Fact]
    public void BuildCommand_MultipleServers_WritesAllUnderMcpServers()
    {
        var options = new ClaudeCodeOptions
        {
            McpServers = new Dictionary<string, IMcpServerConfig>
            {
                ["stdio-srv"] = new McpStdioServerConfig { Command = "node" },
                ["http-srv"] = new McpHttpServerConfig { Url = "https://example.com" },
            },
        };

        Assert.Equal(
            "{\"mcpServers\":{\"stdio-srv\":{\"type\":\"stdio\",\"command\":\"node\"},\"http-srv\":{\"type\":\"http\",\"url\":\"https://example.com\"}}}",
            GetMcpConfigArgument(options)
        );
    }

    [Fact]
    public void BuildCommand_NoMcpServers_OmitsMcpConfigArgument()
    {
        var options = new ClaudeCodeOptions();

        var command = CommandUtil.BuildCommand(options, isStreaming: true, prompt: string.Empty);

        Assert.DoesNotContain("--mcp-config", command);
    }

    [Fact]
    public void BuildCommand_EmptyMcpServers_OmitsMcpConfigArgument()
    {
        var options = new ClaudeCodeOptions
        {
            McpServers = new Dictionary<string, IMcpServerConfig>(),
        };

        var command = CommandUtil.BuildCommand(options, isStreaming: true, prompt: string.Empty);

        Assert.DoesNotContain("--mcp-config", command);
    }

    private static string GetMcpConfigArgument(ClaudeCodeOptions options)
    {
        var command = CommandUtil.BuildCommand(options, isStreaming: true, prompt: string.Empty);
        var index = command.IndexOf("--mcp-config");
        Assert.True(index >= 0);
        return command[index + 1];
    }
}
