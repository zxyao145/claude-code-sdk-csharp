# ClaudeCodeSdk

A .NET SDK for interacting with Claude through the Claude Code CLI. This core package provides both one-shot queries and interactive client sessions for seamless Claude AI integration.

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

    // Model configuration
    Model = "claude-sonnet-4-5",
    SystemPrompt = "You are a helpful assistant.",
    MaxTurns = 10,
    MaxTokens = 4096,

    // Tools and permissions
    AllowedTools = new[] { "Read", "Write", "Bash" },
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
  - Handles Claude Code CLI lifecycle
  - JSON-based message protocol
  - Automatic CLI discovery
  - Shared by both `ClaudeQuery` and `ClaudeSdkClient`

- **`MessageParser`** - Type-safe message parsing
  - Converts JSON to strongly-typed `IMessage` objects
  - Polymorphic content block handling
  - Comprehensive error reporting

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

### Message Termination

- `ClaudeProcess.ReceiveAsync()` automatically terminates when receiving `ResultMessage`
- Both `ClaudeQuery` and `ClaudeSdkClient` rely on this automatic termination
- `ResultMessage` contains usage statistics and session metadata

## Environment Variables

The SDK automatically manages these environment variables:

- **`ANTHROPIC_AUTH_TOKEN`** - Set from `ClaudeCodeOptions.ApiKey`
- **`ANTHROPIC_BASE_URL`** - Set from `ClaudeCodeOptions.BaseUrl`
- **`CLAUDE_CODE_ENTRYPOINT`** - Always set to "sdk-csharp"

Additional custom variables can be set via `ClaudeCodeOptions.EnvironmentVariables`.

## Examples

See the [examples](../../examples/) directory for complete working examples:

- **QuickStartExamples.cs** - Basic usage, options, tools, and interactive client
- **StreamingExamples.cs** - Streaming modes and interactive user input

Run examples:

```bash
dotnet run --project examples/
```

## Testing

Run tests:

```bash
# All tests
dotnet test

# With verbose output
dotnet test --verbosity normal

# Specific test
dotnet test --filter "FullyQualifiedName~ExceptionsTests"
```

## Building

Build the package:

```bash
# Development build
dotnet build src/ClaudeCodeSdk/ClaudeCodeSdk.csproj

# Release build with NuGet package
dotnet pack src/ClaudeCodeSdk/ClaudeCodeSdk.csproj -c Release
```

## Microsoft Agent Framework Integration

For Microsoft Agent Framework support, see the separate [ClaudeCodeSdk.MAF](../ClaudeCodeSdk.MAF/) package.

## License

See [LICENSE.txt](../../LICENSE.txt) for details.

## Related Projects

- [Claude Code CLI](https://github.com/anthropics/claude-code) - The official Claude Code command-line interface
- [ClaudeCodeSdk.MAF](../ClaudeCodeSdk.MAF/) - Microsoft Agent Framework integration

## Support

For issues, questions, or contributions, please visit the [GitHub repository](https://github.com/zxyao145/claude-code-sdk-csharp).
