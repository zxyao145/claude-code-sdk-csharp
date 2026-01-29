# ClaudeCodeSdk

A .NET SDK for interacting with Claude through the Claude Code CLI. This core package provides both one-shot queries and interactive client sessions for seamless Claude AI integration.

## Features

- **Dual-Pattern Architecture**: Choose between one-shot queries (`ClaudeQuery`) or interactive sessions (`ClaudeSdkClient`)
- **Streaming Responses**: Real-time message streaming with `IAsyncEnumerable<T>`
- **Type-Safe Messaging**: Strongly-typed message and content block interfaces
- **Tool Integration**: Full support for Claude Code's built-in tools (Read, Write, Bash, Grep, etc.)
- **Session Management**: Multi-turn conversations with resumption support
- **MCP Servers**: Configure Model Context Protocol servers for extended capabilities
- **Resource Management**: Automatic cleanup with `IAsyncDisposable` pattern
- **Comprehensive Error Handling**: Rich exception hierarchy for debugging

## Installation

Install via NuGet:

```bash
dotnet add package ClaudeCodeSdk
```

## Prerequisites

- .NET 10.0 SDK (supports .NET 8.0+ for compatibility)
- Claude Code CLI: `npm install -g @anthropic-ai/claude-code`
- Anthropic API key (set via environment variable or options)

## Quick Start

### One-shot Query

For simple, fire-and-forget queries:

```csharp
using ClaudeCodeSdk;
using ClaudeCodeSdk.Types;

var options = new ClaudeCodeOptions
{
    ApiKey = "your-api-key" // Or use ANTHROPIC_API_KEY env var
};

await foreach (var message in ClaudeQuery.QueryAsync("What is 2 + 2?", options))
{
    if (message is AssistantMessage assistantMessage)
    {
        foreach (var block in assistantMessage.Content)
        {
            if (block is TextBlock textBlock)
            {
                Console.WriteLine($"Claude: {textBlock.Text}");
            }
        }
    }
}
```

### Interactive Client

For multi-turn conversations with manual lifecycle control:

```csharp
using ClaudeCodeSdk;
using ClaudeCodeSdk.Types;

var options = new ClaudeCodeOptions
{
    ApiKey = "your-api-key",
    SystemPrompt = "You are a helpful coding assistant."
};

await using var client = new ClaudeSdkClient(options);

// Connect to Claude
await client.ConnectAsync();

// Send first message
await client.QueryAsync("What is dependency injection?");

// Receive response
await foreach (var message in client.ReceiveResponseAsync())
{
    if (message is AssistantMessage assistantMessage)
    {
        foreach (var block in assistantMessage.Content)
        {
            if (block is TextBlock textBlock)
            {
                Console.WriteLine($"Claude: {textBlock.Text}");
            }
        }
    }
}

// Send follow-up question (context preserved)
await client.QueryAsync("Show me a C# example");

await foreach (var message in client.ReceiveResponseAsync())
{
    // Handle response...
}
```

## Core Concepts

### Two Usage Patterns

#### 1. ClaudeQuery (One-shot)
- **Use case**: Simple, single-turn interactions
- **Pattern**: Fire-and-forget
- **Lifecycle**: Automatic connection management
- **Method**: `ClaudeQuery.QueryAsync(prompt, options)`

```csharp
// Automatically handles connection lifecycle
await foreach (var message in ClaudeQuery.QueryAsync("Explain async/await"))
{
    // Process messages
}
```

#### 2. ClaudeSdkClient (Interactive)
- **Use case**: Multi-turn conversations, session management
- **Pattern**: Long-lived bidirectional communication
- **Lifecycle**: Manual control via `ConnectAsync/DisconnectAsync`
- **Methods**: `ConnectAsync()`, `QueryAsync()`, `ReceiveResponseAsync()`

```csharp
await using var client = new ClaudeSdkClient(options);
await client.ConnectAsync();

// Multiple queries in same session
await client.QueryAsync("First question");
await foreach (var msg in client.ReceiveResponseAsync()) { /* ... */ }

await client.QueryAsync("Follow-up question");
await foreach (var msg in client.ReceiveResponseAsync()) { /* ... */ }
```

### Message Types

All messages implement `IMessage`:

- **`AssistantMessage`** - Claude's responses with content blocks
- **`UserMessage`** - User input messages
- **`SystemMessage`** - System notifications and metadata
- **`ResultMessage`** - End-of-conversation marker with usage statistics

```csharp
await foreach (var message in ClaudeQuery.QueryAsync("Hello"))
{
    switch (message)
    {
        case AssistantMessage assistant:
            // Handle Claude's response
            foreach (var block in assistant.Content)
            {
                // Process content blocks
            }
            break;

        case ResultMessage result:
            // Session complete - check usage/cost
            Console.WriteLine($"Cost: ${result.TotalCostUsd}");
            Console.WriteLine($"Turns: {result.NumTurns}");
            break;

        case SystemMessage system:
            // Handle system notifications
            break;
    }
}
```

### Content Blocks

All content blocks implement `IContentBlock`:

- **`TextBlock`** - Plain text content from Claude
- **`ThinkingBlock`** - Claude's reasoning process (when enabled)
- **`ToolUseBlock`** - Tool invocation requests
- **`ToolResultBlock`** - Tool execution results
- **`ErrorContentBlock`** - Error information

```csharp
if (message is AssistantMessage assistantMessage)
{
    foreach (var block in assistantMessage.Content)
    {
        switch (block)
        {
            case TextBlock text:
                Console.WriteLine($"Text: {text.Text}");
                break;

            case ThinkingBlock thinking:
                Console.WriteLine($"Thinking: {thinking.Thinking}");
                break;

            case ToolUseBlock toolUse:
                Console.WriteLine($"Using tool: {toolUse.Name}");
                Console.WriteLine($"Input: {toolUse.Input}");
                break;

            case ToolResultBlock toolResult:
                Console.WriteLine($"Tool result: {toolResult.Content}");
                break;

            case ErrorContentBlock error:
                Console.WriteLine($"Error: {error.Message}");
                break;
        }
    }
}
```

## Configuration

### ClaudeCodeOptions

Configure Claude's behavior with `ClaudeCodeOptions`:

```csharp
var options = new ClaudeCodeOptions
{
    // Authentication
    ApiKey = "your-api-key",  // Or set ANTHROPIC_API_KEY env var
    BaseUrl = "https://api.anthropic.com",  // Custom API endpoint (optional)

    // Model configuration
    Model = "claude-sonnet-4-5",
    SystemPrompt = "You are a helpful assistant.",
    MaxTurns = 10,
    MaxTokens = 4096,

    // Tools and permissions
    AllowedTools = new[] { "Read", "Write", "Bash", "Grep" },
    BlockedTools = new[] { "WebSearch" },
    PermissionMode = PermissionMode.Allow,  // or Deny/Ask

    // Session management
    Resume = "session-id",  // Resume previous session
    WorkingDirectory = "/path/to/project",

    // Advanced options
    EnableThinking = true,  // Show Claude's reasoning
    CliPath = "/custom/path/to/claude-code",
    EnvironmentVariables = new Dictionary<string, string>
    {
        ["CUSTOM_VAR"] = "value"
    }
};
```

#### Permission Modes

- **`PermissionMode.Allow`** - Auto-approve all tool executions
- **`PermissionMode.Deny`** - Auto-deny all tool executions (read-only mode)
- **`PermissionMode.Ask`** - Prompt user for each tool execution (default)

### Using Tools

Enable Claude to use built-in tools:

```csharp
var options = new ClaudeCodeOptions
{
    AllowedTools = new[] { "Read", "Write", "Bash", "Grep" },
    SystemPrompt = "You are a file management assistant.",
    WorkingDirectory = Directory.GetCurrentDirectory()
};

await foreach (var message in ClaudeQuery.QueryAsync(
    "Create a hello.txt file with 'Hello World'",
    options))
{
    if (message is AssistantMessage assistantMessage)
    {
        foreach (var block in assistantMessage.Content)
        {
            if (block is ToolUseBlock toolUse)
            {
                Console.WriteLine($"Using tool: {toolUse.Name}");
            }
            else if (block is TextBlock textBlock)
            {
                Console.WriteLine($"Claude: {textBlock.Text}");
            }
        }
    }
}
```

### MCP Server Configuration

Configure Model Context Protocol (MCP) servers:

```csharp
var options = new ClaudeCodeOptions
{
    McpServers = new IMcpServerConfig[]
    {
        new McpStdioServerConfig
        {
            Name = "my-server",
            Command = "node",
            Args = new[] { "/path/to/server.js" }
        },
        new McpSSEServerConfig
        {
            Name = "sse-server",
            Url = "http://localhost:3000/sse"
        },
        new McpHttpServerConfig
        {
            Name = "http-server",
            Url = "http://localhost:3000/messages"
        }
    }
};
```

## Exception Handling

The SDK provides a comprehensive exception hierarchy:

```csharp
using ClaudeCodeSdk.Exceptions;

try
{
    await foreach (var message in ClaudeQuery.QueryAsync("Hello"))
    {
        // Process messages
    }
}
catch (CLINotFoundException ex)
{
    // Claude Code CLI not found - install it
    Console.WriteLine("Please install: npm install -g @anthropic-ai/claude-code");
}
catch (CLIConnectionException ex)
{
    // Connection/transport issues
    Console.WriteLine($"Connection error: {ex.Message}");
}
catch (ProcessException ex)
{
    // Subprocess execution failures
    Console.WriteLine($"Process error: {ex.Message}");
}
catch (CLIJsonDecodeException ex)
{
    // JSON parsing errors
    Console.WriteLine($"Invalid JSON from CLI: {ex.Message}");
}
catch (MessageParseException ex)
{
    // Message type conversion failures
    Console.WriteLine($"Message parsing error: {ex.Message}");
}
catch (ClaudeSDKException ex)
{
    // Base exception - catches all SDK errors
    Console.WriteLine($"SDK error: {ex.Message}");
}
```

## Advanced Usage

### Session Resumption

Resume previous conversations:

```csharp
// First session
var options = new ClaudeCodeOptions();
await using var client1 = new ClaudeSdkClient(options);
await client1.ConnectAsync();
await client1.QueryAsync("Remember this: my name is Alice");

string? sessionId = null;
await foreach (var msg in client1.ReceiveResponseAsync())
{
    if (msg is ResultMessage result)
    {
        sessionId = result.SessionId;
    }
}

// Resume later
var resumeOptions = new ClaudeCodeOptions
{
    Resume = sessionId
};
await using var client2 = new ClaudeSdkClient(resumeOptions);
await client2.ConnectAsync();
await client2.QueryAsync("What's my name?"); // Claude remembers: "Alice"
```

### Streaming Input Messages

Stream multiple messages to Claude:

```csharp
async IAsyncEnumerable<Dictionary<string, object>> CreateMessageStream()
{
    yield return new Dictionary<string, object>
    {
        ["type"] = "user",
        ["message"] = new Dictionary<string, object>
        {
            ["role"] = "user",
            ["content"] = "First message"
        }
    };

    await Task.Delay(100);

    yield return new Dictionary<string, object>
    {
        ["type"] = "user",
        ["message"] = new Dictionary<string, object>
        {
            ["role"] = "user",
            ["content"] = "Second message"
        }
    };
}

var messages = CreateMessageStream();
await foreach (var response in ClaudeQuery.QueryAsync(messages))
{
    // Handle responses
}
```

### Enabling Thinking Mode

See Claude's reasoning process:

```csharp
var options = new ClaudeCodeOptions
{
    EnableThinking = true
};

await foreach (var message in ClaudeQuery.QueryAsync("Solve this puzzle...", options))
{
    if (message is AssistantMessage assistantMessage)
    {
        foreach (var block in assistantMessage.Content)
        {
            if (block is ThinkingBlock thinking)
            {
                Console.WriteLine($"[Thinking] {thinking.Thinking}");
            }
            else if (block is TextBlock text)
            {
                Console.WriteLine($"[Answer] {text.Text}");
            }
        }
    }
}
```

## Architecture

### Core Components

- **`ClaudeProcess`** - Unified subprocess manager
  - Handles Claude Code CLI lifecycle (start, communication, cleanup)
  - JSON-based message protocol with strongly-typed parsing
  - Automatic CLI discovery via `CommandUtil`
  - Shared by both `ClaudeQuery` and `ClaudeSdkClient`

- **`ClaudeQuery`** - One-shot query API
  - Fire-and-forget pattern for simple interactions
  - Automatic connection lifecycle management
  - Returns `IAsyncEnumerable<IMessage>` for streaming

- **`ClaudeSdkClient`** - Interactive client API
  - Long-lived sessions with manual lifecycle control
  - Multi-turn conversation support
  - Methods: `ConnectAsync()`, `DisconnectAsync()`, `QueryAsync()`, `ReceiveResponseAsync()`

- **`MessageParser`** - Type-safe message parsing
  - Converts JSON to strongly-typed `IMessage` objects
  - Handles polymorphic content blocks
  - Comprehensive error reporting with line numbers

- **`JsonUtil`** - Serialization helpers
  - `snake_case_lower` naming policy for CLI compatibility
  - Consistent across all message exchanges

### Resource Management

All process-managing classes implement `IAsyncDisposable`:

```csharp
// Recommended: automatic cleanup
await using var client = new ClaudeSdkClient(options);
await client.ConnectAsync();
// ... use client ...
// Automatically cleaned up when scope exits

// Manual cleanup if needed
var client = new ClaudeSdkClient(options);
try
{
    await client.ConnectAsync();
    // ... use client ...
}
finally
{
    await client.DisposeAsync();
}
```

**Key Point**: The SDK automatically manages subprocess lifecycle:
- Process is started on `ConnectAsync()` or first `QueryAsync()`
- Stdin/stdout streams are properly closed on disposal
- Process handle is cleaned up to prevent resource leaks

### Message Streaming and Termination

The SDK uses automatic message termination:

1. **`ClaudeProcess.ReceiveAsync()`** continuously reads messages from the CLI
2. When a `ResultMessage` (type="result") is received, the stream automatically terminates
3. Both `ClaudeQuery` and `ClaudeSdkClient` rely on this behavior
4. `ClaudeSdkClient.ReceiveResponseAsync()` provides convenience wrapper that yields until `ResultMessage`

```csharp
// This loop automatically terminates when ResultMessage is received
await foreach (var message in client.ReceiveResponseAsync())
{
    // Process messages...
    // No need to manually check for ResultMessage
}
```

### Message Protocol

The SDK uses a JSON-based message protocol over stdin/stdout:

**Request Format** (sent to CLI):
```json
{
  "type": "user",
  "message": {
    "role": "user",
    "content": "Your prompt here"
  }
}
```

**Response Format** (received from CLI):
```json
{
  "type": "assistant",
  "message": {
    "role": "assistant",
    "content": [
      {
        "type": "text",
        "text": "Response here"
      }
    ]
  }
}
```

All property names use `snake_case_lower` to match the CLI protocol.

## Environment Variables

### Managed by SDK

The SDK automatically sets these environment variables for the Claude Code CLI process:

- **`ANTHROPIC_AUTH_TOKEN`** - Set from `ClaudeCodeOptions.ApiKey`
- **`ANTHROPIC_BASE_URL`** - Set from `ClaudeCodeOptions.BaseUrl` (if provided)
- **`CLAUDE_CODE_ENTRYPOINT`** - Always set to "sdk-csharp" for SDK identification

### User-Defined Variables

Additional environment variables can be set via `ClaudeCodeOptions.EnvironmentVariables`:

```csharp
var options = new ClaudeCodeOptions
{
    EnvironmentVariables = new Dictionary<string, string?>
    {
        ["HTTP_PROXY"] = "http://proxy.example.com:8080",
        ["HTTPS_PROXY"] = "http://proxy.example.com:8080",
        ["MY_CUSTOM_VAR"] = "custom-value"
    }
};
```

## Examples

See the [examples](../../examples/) directory for complete working examples:

- **QuickStartExamples.cs** - Basic usage, options configuration, tools, and interactive client
- **StreamingExamples.cs** - Streaming modes, input streaming, and interactive user input

Run examples:

```bash
dotnet run --project examples/
```

### Example: Tool Integration

```csharp
using ClaudeCodeSdk;
using ClaudeCodeSdk.Types;

var options = new ClaudeCodeOptions
{
    AllowedTools = new[] { "Read", "Write", "Bash", "Grep" },
    PermissionMode = PermissionMode.Allow,  // Auto-approve tools
    WorkingDirectory = Directory.GetCurrentDirectory()
};

await foreach (var message in ClaudeQuery.QueryAsync(
    "Create a hello.txt file with 'Hello World'",
    options))
{
    if (message is AssistantMessage assistantMessage)
    {
        foreach (var block in assistantMessage.Content)
        {
            if (block is ToolUseBlock toolUse)
            {
                Console.WriteLine($"Tool: {toolUse.Name}");
                Console.WriteLine($"Input: {toolUse.Input}");
            }
            else if (block is TextBlock textBlock)
            {
                Console.WriteLine($"Claude: {textBlock.Text}");
            }
        }
    }
}
```

### Example: Multi-Turn Conversation

```csharp
await using var client = new ClaudeSdkClient(options);
await client.ConnectAsync();

// First question
await client.QueryAsync("What is dependency injection?");
await foreach (var msg in client.ReceiveResponseAsync())
{
    if (msg is AssistantMessage assistant)
        Console.WriteLine(assistant.Content.GetText());
}

// Follow-up question (context preserved)
await client.QueryAsync("Show me a C# example");
await foreach (var msg in client.ReceiveResponseAsync())
{
    if (msg is AssistantMessage assistant)
        Console.WriteLine(assistant.Content.GetText());
}
```

## Testing

Run tests:

```bash
# All tests
dotnet test

# With verbose output
dotnet test --verbosity normal

# Specific test
dotnet test --filter "FullyQualifiedName~MessageParsingTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

Test organization:
- **`TypesTests.cs`** - Message type system validation
- **`ExceptionsTests.cs`** - Exception handling verification
- **`MessageParsingTests.cs`** - JSON parsing and conversion

## Building

Build the package:

```bash
# Development build
dotnet build src/ClaudeCodeSdk/ClaudeCodeSdk.csproj

# Release build with NuGet package
dotnet pack src/ClaudeCodeSdk/ClaudeCodeSdk.csproj -c Release

# Build all projects in solution
dotnet build claude-code-sdk-csharp.sln
```

Output packages:
- `src/ClaudeCodeSdk/bin/Release/*.nupkg` - Core SDK package
- `src/ClaudeCodeSdk.MAF/bin/Release/*.nupkg` - MAF integration package

## Microsoft Agent Framework Integration

For Microsoft Agent Framework (MAF) support, see the separate [ClaudeCodeSdk.MAF](../ClaudeCodeSdk.MAF/) package.

### Key Features of MAF Integration

- **`ClaudeCodeAIAgent`** - Implements MAF's `AIAgent` interface
- **`ClaudeCodeAgentThread`** - Thread management with session ID persistence
- **`ClaudeSdkClientManager`** - Manages client lifecycle across sessions
- **Streaming Support** - Both `RunAsync()` and `RunStreamingAsync()` available

Install MAF package:

```bash
dotnet add package ClaudeCodeSdk.MAF
```

## Troubleshooting

### CLI Not Found

**Error**: `CLINotFoundException`

**Solution**:
```bash
npm install -g @anthropic-ai/claude-code
```

### Connection Issues

**Error**: `CLIConnectionException`

**Possible Causes**:
- CLI process failed to start
- Invalid CLI path (use `CliPath` option to specify custom path)
- Environment blocking subprocess execution

**Solution**:
```csharp
var options = new ClaudeCodeOptions
{
    CliPath = @"C:\full\path\to\claude-code.cmd"  // Windows
    // or
    CliPath = "/usr/local/bin/claude-code"  // Linux/macOS
};
```

### JSON Parsing Errors

**Error**: `CLIJsonDecodeException` or `MessageParseException`

**Possible Causes**:
- CLI returned malformed JSON
- Unsupported message format
- SDK version incompatible with CLI version

**Solution**:
- Update SDK: `dotnet add package ClaudeCodeSdk --version <latest>`
- Update CLI: `npm update -g @anthropic-ai/claude-code`
- Check error details in exception message for line/column numbers

### Process Resource Leaks

**Symptom**: Zombie `claude-code` processes

**Solution**:
- Always use `await using` for `ClaudeSdkClient` or `ClaudeQuery`
- Call `DisposeAsync()` explicitly if not using `await using`
- Ensure all async operations are properly awaited

```csharp
// Correct
await using var client = new ClaudeSdkClient(options);
await client.ConnectAsync();

// Incorrect - resource leak
var client = new ClaudeSdkClient(options);  // Never disposed
```

## Version History

### 1.0.0-preview (Latest)
- Initial public preview release
- Unified `ClaudeProcess` architecture
- Dual-pattern API (ClaudeQuery + ClaudeSdkClient)
- Type-safe message parsing with polymorphic content blocks
- Tool integration support
- Session resumption and multi-turn conversations
- MCP server configuration
- Comprehensive exception hierarchy
- Full async/await support with `IAsyncDisposable`

## Contributing

Contributions are welcome! Please follow these guidelines:

1. **Code Style**: Follow .NET coding conventions (PascalCase for types, camelCase for parameters)
2. **Tests**: Add unit tests for new features
3. **Documentation**: Update XML documentation comments
4. **Commits**: Use Conventional Commits format (`feat:`, `fix:`, `docs:`, etc.)

## License

See [LICENSE.txt](../../LICENSE.txt) for details.

## Related Projects

- **[Claude Code CLI](https://github.com/anthropics/claude-code)** - The official Claude Code command-line interface
- **[ClaudeCodeSdk.MAF](../ClaudeCodeSdk.MAF/)** - Microsoft Agent Framework integration
- **[D-System](https://github.com/zxyao145/D-System)** - ASP.NET Core backend using this SDK for agent management

## Support

- **Issues**: [GitHub Issues](https://github.com/zxyao145/claude-code-sdk-csharp/issues)
- **Discussions**: [GitHub Discussions](https://github.com/zxyao145/claude-code-sdk-csharp/discussions)
- **Documentation**: [Project README](../../README.md)

## Acknowledgments

Built on top of the excellent [Claude Code CLI](https://github.com/anthropics/claude-code) by Anthropic.

---

**Note**: This SDK is not officially affiliated with Anthropic. It is a community-maintained project for .NET developers.
