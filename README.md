# Claude Code SDK for C#

A .NET SDK for interacting with Claude through the Claude Code CLI, providing both one-shot queries and interactive client sessions with full Microsoft Agent Framework (MAF) integration.

## Features

- ✅ **One-shot Queries** - Simple request/response pattern via `ClaudeQuery.QueryAsync`
- ✅ **Interactive Client** - Bidirectional communication with `ClaudeSdkClient`
- ✅ **Streaming Support** - Real-time response streaming via `IAsyncEnumerable<T>`
- ✅ **Microsoft Agent Framework Integration** - Use Claude as an `AIAgent` (ClaudeCodeSdk.MAF)
- ✅ **Session Management** - Multi-turn conversations with thread support and automatic session lifecycle management
- ✅ **Thread Persistence** - Serialize/deserialize conversation threads for storage
- ✅ **Tool Integration** - Full support for Claude Code tools and MCP servers
- ✅ **Thinking Blocks** - Extended reasoning with configurable thinking tokens
- ✅ **Usage Tracking** - Token usage and cost monitoring
- ✅ **.NET 10.0 Ready** - Built on the latest .NET platform (compatible with .NET 8.0+)
- ✅ **Modern Async/Await** - Full `IAsyncDisposable` support with proper resource management

## Version

**Current Version**: `0.10.1-preview1`

## Status

[![CI/CD](https://github.com/zxyao145/claude-code-sdk-csharp/workflows/CI/badge.svg)](https://github.com/zxyao145/claude-code-sdk-csharp/actions)

> [!NOTE]
> This is a preview release. APIs may change between versions.

## Installation

### Core SDK

Install via NuGet:

```bash
dotnet add package ClaudeCodeSdk
```

### Microsoft Agent Framework Integration (Optional)

For Microsoft Agent Framework support:

```bash
dotnet add package ClaudeCodeSdk.MAF
```

## Prerequisites

- **.NET 10.0 SDK** 
- **Claude Code CLI**: Install globally via npm:
  ```bash
  npm install -g @anthropic-ai/claude-code
  ```
- **API Key**: Set `ANTHROPIC_AUTH_TOKEN` environment variable with your Anthropic API key

## Quick Start

### One-shot Query

```csharp
using ClaudeCodeSdk;

var messages = ClaudeQuery.QueryAsync("What is the capital of France?");
await foreach (var message in messages)
{
    Console.WriteLine(message.Content);
}
```

### Interactive Client

```csharp
using ClaudeCodeSdk;

using var client = new ClaudeSdkClient();
await client.ConnectAsync();

await client.SendMessageAsync("Hello Claude!");

await foreach (var message in client.ReceiveMessagesAsync())
{
    if (message is AssistantMessage assistantMsg)
    {
        Console.WriteLine($"Claude: {assistantMsg.Content}");
    }
}
```

### Microsoft Agent Framework Integration

```csharp
using ClaudeCodeSdk.MAF;
using Microsoft.Extensions.AI;

// Create Claude as an AIAgent
await using var agent = new ClaudeCodeAIAgent();

// Simple query
var response = await agent.RunAsync("Explain async/await in C#");
Console.WriteLine(response.Text);

// Multi-turn conversation with thread
var thread = agent.GetNewThread();
var response1 = await agent.RunAsync(
    [new ChatMessage(ChatRole.User, "What is dependency injection?")],
    thread: thread
);

// Context is automatically preserved across turns
var response2 = await agent.RunAsync(
    [new ChatMessage(ChatRole.User, "Show me an example in C#")],
    thread: thread
);

// Streaming with real-time updates
await foreach (var update in agent.RunStreamingAsync("Tell me a story", thread: thread))
{
    if (update.Contents != null)
    {
        foreach (var content in update.Contents)
        {
            if (content is TextContent text)
            {
                Console.Write(text.Text);
            }
        }
    }
}
```

See [ClaudeCodeSdk.MAF README](src/ClaudeCodeSdk.MAF/README.md) for complete MAF integration documentation.

## Architecture

The SDK implements a simplified dual-pattern architecture for Claude Code interactions:

### Core Components

**ClaudeProcess** - Unified subprocess manager
- Direct subprocess communication with Claude Code CLI
- JSON-based message protocol with strongly-typed parsing
- Automatic CLI discovery and process lifecycle management
- Shared by both `ClaudeQuery` and `ClaudeSdkClient`

**ClaudeQuery** - One-shot query API
- Fire-and-forget pattern for simple queries
- Streams responses as `IAsyncEnumerable<IMessage>`
- Automatically handles connection lifecycle
- Ideal for single-request scenarios

**ClaudeSdkClient** - Interactive client API
- Long-lived bidirectional communication
- Manual connection control via `ConnectAsync/DisconnectAsync`
- Session management with multi-turn conversations
- Interrupt support and resource cleanup

### Message Types

All messages implement `IMessage`:

- `AssistantMessage` - Claude's responses with content blocks
- `UserMessage` - User input
- `SystemMessage` - System notifications and metadata
- `ResultMessage` - End-of-conversation marker with cost/usage data

### Content Blocks

All content blocks implement `IContentBlock`:

- `TextBlock` - Plain text content
- `ThinkingBlock` - Claude's reasoning (when extended thinking is enabled)
- `ToolUseBlock` - Tool invocations
- `ToolResultBlock` - Tool execution results
- `ErrorContentBlock` - Error information

### Exception Hierarchy

Custom exceptions inherit from `ClaudeSDKException`:

- `CLINotFoundException` - Claude Code CLI not found
- `CLIConnectionException` - Transport connection issues
- `ProcessException` - Subprocess execution failures
- `CLIJsonDecodeException` - Message parsing errors
- `MessageParseException` - Type conversion failures

## Microsoft Agent Framework Integration

The MAF integration (`ClaudeCodeSdk.MAF`) provides:

### ClaudeCodeAIAgent
- Full `AIAgent` implementation from Microsoft.Agents.AI
- Streaming and non-streaming execution modes
- Thread-based conversation management
- Automatic session persistence via `ClaudeSdkClientManager`
- System prompt extraction and configuration

### ClaudeCodeAgentThread
- Thread serialization/deserialization for persistence
- Session ID management for conversation continuity
- Compatible with MAF's `AIConversationState`

### ClaudeSdkClientManager
- Automatic client lifecycle management
- Disposes old clients when switching sessions
- Thread-safe with proper async resource management
- Optimizes resource usage across multiple threads

## Configuration

### Environment Variables

The SDK automatically configures these environment variables for the Claude Code CLI:

- `ANTHROPIC_AUTH_TOKEN` - API authentication (from `ClaudeCodeOptions.ApiKey` or environment)
- `ANTHROPIC_BASE_URL` - Custom API endpoint (from `ClaudeCodeOptions.BaseUrl`)
- `CLAUDE_CODE_ENTRYPOINT` - SDK identifier (always "sdk-csharp")

### ClaudeCodeOptions

Key configuration options:

```csharp
var options = new ClaudeCodeOptions
{
    ApiKey = "sk-ant-...",              // Anthropic API key
    BaseUrl = "https://api.anthropic.com", // Custom API endpoint
    MaxThinkingTokens = 10000,          // Extended thinking budget
    SystemPrompt = "You are a helpful assistant",
    Model = "claude-sonnet-4",
    PermissionMode = PermissionMode.Auto, // Tool approval mode
    WorkingDirectory = "/path/to/project",
    MaxTurns = 10,
    EnvironmentVariables = new Dictionary<string, string?>
    {
        { "HTTP_PROXY", "http://proxy:8080" }
    }
};
```

## Development

### Building

```bash
# Build the entire solution
dotnet build

# Build specific project
dotnet build src/ClaudeCodeSdk/ClaudeCodeSdk.csproj
```

### Testing

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run specific test
dotnet test --filter "FullyQualifiedName~ExceptionsTests"
```

### Running Examples

```bash
# Run all examples
dotnet run --project examples/ClaudeCodeSdk.Examples.csproj

# Run specific example class
dotnet run --project examples/ClaudeCodeSdk.Examples.csproj -- --example QuickStart
```

### Packaging NuGet Packages

```bash
# Pack both SDK and MAF packages
dotnet pack src/ClaudeCodeSdk/ClaudeCodeSdk.csproj -c Release
dotnet pack src/ClaudeCodeSdk.MAF/ClaudeCodeSdk.MAF.csproj -c Release

# Pack with symbols
dotnet pack src/ClaudeCodeSdk/ClaudeCodeSdk.csproj -c Release -p:IncludeSymbols=true
```

## Examples

The `examples/` folder contains complete working examples:

- **QuickStartExamples** - Basic one-shot queries and interactive client usage
- **StreamingExamples** - Real-time response streaming patterns
- **MafExample** - Microsoft Agent Framework integration examples

### Running Examples

Make sure you have:
1. Installed Claude Code CLI: `npm install -g @anthropic-ai/claude-code`
2. Set `ANTHROPIC_AUTH_TOKEN` environment variable

Then run:
```bash
dotnet run --project examples/ClaudeCodeSdk.Examples.csproj
```

## Key Behaviors

### Message Streaming and Termination
- `ClaudeProcess.ReceiveAsync()` automatically terminates when receiving a `ResultMessage`
- Both `ClaudeQuery` and `ClaudeSdkClient` rely on this automatic termination
- `ClaudeSdkClient.ReceiveResponseAsync()` provides convenience method that yields until ResultMessage

### JSON Serialization
- Uses `snake_case_lower` naming policy via `JsonUtil` for Claude Code CLI compatibility
- Consistent serialization across all message exchanges

### Resource Management
- All process-managing classes implement `IAsyncDisposable`
- `ClaudeProcess` handles subprocess lifecycle (start, kill, cleanup)
- Use `await using` for automatic cleanup

### MAF Session Management
- `ClaudeSdkClientManager` automatically handles client creation/disposal when switching threads
- Thread session IDs map to Claude Code's `Resume` parameter for conversation continuity
- Session state persists via the thread's `SessionId`

## Troubleshooting

### "Claude Code CLI not found"
Ensure Claude Code CLI is installed globally:
```bash
npm install -g @anthropic-ai/claude-code
```

### Authentication Errors
Set your API key:
```bash
# macOS/Linux
export ANTHROPIC_AUTH_TOKEN="your-api-key"

# Windows PowerShell
$env:ANTHROPIC_AUTH_TOKEN="your-api-key"

# Windows Command Prompt
set ANTHROPIC_AUTH_TOKEN=your-api-key
```

Or pass it via options:
```csharp
var options = new ClaudeCodeOptions { ApiKey = "your-api-key" };
```

### Process Lifecycle Issues
Always dispose of SDK objects properly:
```csharp
await using var client = new ClaudeSdkClient();
await client.ConnectAsync();
// ... use client
// Automatic disposal on scope exit
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

MIT License - see LICENSE.txt for details

## Links

- [Claude Code Documentation](https://docs.anthropic.com/en/docs/claude-code)
- [Claude API Documentation](https://docs.anthropic.com/en/api/getting-started)
- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/dotnet/ai/extending-ai-framework)
- [GitHub Repository](https://github.com/zxyao145/claude-code-sdk-csharp)