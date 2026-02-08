# ClaudeCodeSdk.MAF

Microsoft Agent Framework (MAF) integration for ClaudeCodeSdk, providing an `AIAgent` implementation for Claude Code interactions.

## Overview

This package integrates ClaudeCodeSdk with Microsoft Agent Framework, enabling you to use Claude Code as an AIAgent with full support for:

- ✅ Streaming and non-streaming responses
- ✅ Multi-turn conversations with automatic session management
- ✅ Thread serialization/deserialization for persistence
- ✅ Tool use (function calling) support
- ✅ Thinking/reasoning content blocks
- ✅ Usage tracking and cost monitoring
- ✅ Compatible with Microsoft.Extensions.AI interfaces

## Installation

```bash
dotnet add package ClaudeCodeSdk.MAF
```

## Requirements

- .NET 8.0 or later
- Claude Code CLI installed (`npm install -g @anthropic-ai/claude-code`)
- ANTHROPIC_AUTH_TOKEN environment variable set with your API key

## Quick Start

### Basic Usage

```csharp
using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;

// Create the Claude Code AI Agent
var agent = new ClaudeCodeAIAgent();

// Send a simple query
var response = await agent.RunAsync("Hello! Can you help me with C# programming?");
Console.WriteLine(response.Text);
```

### Streaming Responses

```csharp
using ClaudeCodeSdk.MAF;

var agent = new ClaudeCodeAIAgent();

await foreach (var update in agent.RunStreamingAsync("Explain async/await in C#"))
{
    if (update.Contents != null)
    {
        foreach (var content in update.Contents)
        {
            if (content is TextContent textContent)
            {
                Console.Write(textContent.Text);
            }
        }
    }
}
```

### Using System Messages for Custom Prompts

You can pass System messages to customize Claude's behavior for a specific request:

```csharp
using ClaudeCodeSdk.MAF;
using Microsoft.Extensions.AI;

var agent = new ClaudeCodeAIAgent();

var messages = new[]
{
    new ChatMessage(ChatRole.System, "You are a helpful C# expert who explains concepts simply."),
    new ChatMessage(ChatRole.User, "What is dependency injection?")
};

var response = await agent.RunAsync(messages);
Console.WriteLine(response.Text);
```

**Note**: System messages are automatically extracted and set as the `SystemPrompt` in `ClaudeCodeOptions`. They are not sent as part of the conversation messages.

### Multi-turn Conversation with Thread

```csharp
using ClaudeCodeSdk.MAF;
using Microsoft.Extensions.AI;

var agent = new ClaudeCodeAIAgent();

// Create a new thread for the conversation
var thread = agent.GetNewThread();

// First turn
var response1 = await agent.RunAsync(
    [new ChatMessage(ChatRole.User, "What is dependency injection?")],
    thread: thread
);
Console.WriteLine($"Claude: {response1.Text}");

// Second turn - context is preserved via session ID
var response2 = await agent.RunAsync(
    [new ChatMessage(ChatRole.User, "Can you show me an example in C#?")],
    thread: thread
);
Console.WriteLine($"Claude: {response2.Text}");
```

### Thread Persistence

```csharp
using System.Text.Json;
using ClaudeCodeSdk.MAF;
using Microsoft.Extensions.AI;

var agent = new ClaudeCodeAIAgent();
var thread = agent.GetNewThread();

// Have a conversation
await agent.RunAsync([new ChatMessage(ChatRole.User, "Hello!")], thread: thread);

// Serialize the thread for later use
var serialized = await thread.SerializeAsync();
var json = JsonSerializer.Serialize(serialized);
// Save json to database or file...

// Later: Deserialize and resume
var restored = JsonSerializer.Deserialize<JsonElement>(json);
var restoredThread = agent.DeserializeThread(restored);

// Continue the conversation
var response = await agent.RunAsync(
    [new ChatMessage(ChatRole.User, "What did we talk about?")],
    thread: restoredThread
);
Console.WriteLine(response.Text);
```

### Advanced Configuration

```csharp
using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;
using Microsoft.Extensions.Logging;

// Configure Claude Code options
var options = new ClaudeCodeAIAgentOptions
{
    MaxThinkingTokens = 10000,
    SystemPrompt = "You are an expert C# developer.",
    Model = "claude-sonnet-4",
    PermissionMode = PermissionMode.Auto,
    ApiKey = "your-api-key" // Or set ANTHROPIC_AUTH_TOKEN environment variable
};

// Optional: Add logging
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ClaudeCodeAIAgent>();

var agent = new ClaudeCodeAIAgent(options, logger);

var response = await agent.RunAsync("Help me optimize this LINQ query...");
Console.WriteLine(response.Text);
```

### Working with Tool Calls

When Claude uses tools, the content is automatically converted to MAF types:

```csharp
using ClaudeCodeSdk.MAF;
using Microsoft.Extensions.AI;

var agent = new ClaudeCodeAIAgent();

await foreach (var update in agent.RunStreamingAsync("What files are in the current directory?"))
{
    if (update.Contents != null)
    {
        foreach (var content in update.Contents)
        {
            switch (content)
            {
                case TextContent text:
                    Console.WriteLine($"Text: {text.Text}");
                    break;
                case FunctionCallContent funcCall:
                    Console.WriteLine($"Tool: {funcCall.Name}");
                    break;
                case FunctionResultContent result:
                    Console.WriteLine($"Tool Result: {result.Result}");
                    break;
                case TextReasoningContent reasoning:
                    Console.WriteLine($"Thinking: {reasoning.Text}");
                    break;
            }
        }
    }
}
```

### Monitoring Usage and Costs

```csharp
using ClaudeCodeSdk.MAF;
using Microsoft.Extensions.AI;

var agent = new ClaudeCodeAIAgent();

var response = await agent.RunAsync("Explain recursion");

// Access usage information
if (response.Usage != null)
{
    Console.WriteLine($"Input tokens: {response.Usage.InputTokenCount}");
    Console.WriteLine($"Output tokens: {response.Usage.OutputTokenCount}");
    Console.WriteLine($"Cached tokens: {response.Usage.CachedInputTokenCount}");

    if (response.Usage.AdditionalCounts != null)
    {
        Console.WriteLine($"Cache read tokens: {response.Usage.AdditionalCounts["cacheReadInputTokens"]}");
    }
}
```

## Architecture

### ClaudeCodeAIAgent

Main class implementing `AIAgent` from Microsoft.Agents.AI:

- **GetNewThread()** - Creates a new conversation thread
- **RunAsync()** - Execute a query and get a complete response
- **RunStreamingAsync()** - Execute a query with streaming updates
- **DeserializeThread()** - Restore a persisted conversation thread

### ClaudeCodeAgentThread

Internal thread implementation that:
- Maintains session ID for conversation continuity
- Automatically captures session ID from first system message
- Supports serialization for persistence (stores session ID)

### ClaudeCodeAIAgentOptions

Configuration options wrapper that extends ClaudeCodeOptions:

| Property | Description |
|----------|-------------|
| `MaxThinkingTokens` | Maximum tokens for Claude's reasoning (default: 8000) |
| `SystemPrompt` | Custom system prompt |
| `AppendSystemPrompt` | Additional system prompt to append |
| `Model` | Claude model to use (e.g., "claude-sonnet-4") |
| `PermissionMode` | Tool permission mode (Auto, Prompt, Deny) |
| `AllowedTools` | List of allowed tools |
| `DisallowedTools` | List of disallowed tools |
| `McpServers` | MCP server configurations |
| `McpServersPath` | Path to MCP servers configuration file |
| `MaxTurns` | Maximum conversation turns |
| `WorkingDirectory` | Working directory for Claude Code CLI |
| `Settings` | Path to settings file |
| `AddDirectories` | Additional directories to include |
| `ApiKey` | Anthropic API key (overrides ANTHROPIC_AUTH_TOKEN) |
| `BaseUrl` | Custom API endpoint (overrides ANTHROPIC_BASE_URL) |
| `EnvironmentVariables` | Additional environment variables |

## Content Type Conversions

The integration automatically converts between Claude Code content blocks and MAF AIContent types:

| Claude Content Block | MAF AIContent Type |
|---------------------|-------------------|
| `TextBlock` | `TextContent` |
| `ThinkingBlock` | `TextReasoningContent` |
| `ToolUseBlock` | `FunctionCallContent` |
| `ToolResultBlock` | `FunctionResultContent` |
| `ErrorContentBlock` | `ErrorContent` |

## Key Behaviors

### System Message Handling
- System messages in the input are extracted and set as `SystemPrompt` in `ClaudeCodeOptions`
- They are not sent as part of the conversation messages to Claude Code CLI
- Only the first system message is used if multiple are provided

### Session Management
- Each thread maintains a `SessionId` that maps to Claude Code's session persistence
- The session ID is automatically captured from the first `SystemMessage` received
- Multi-turn conversations use the session ID via the `Resume` parameter
- Threads can be serialized/deserialized with their session ID preserved

### Connection Lifecycle
- Each `RunAsync()` or `RunStreamingAsync()` call creates a new `ClaudeSdkClient` connection
- Connections are automatically disposed after each turn
- Thread session state persists across connections

### Message Processing
- **RunAsync()** - Collects all messages until `ResultMessage` and returns complete response
- **RunStreamingAsync()** - Yields `AgentRunResponseUpdate` for each message received
- Only user messages are sent to Claude Code (system messages become system prompts)

## Important Notes

- **Resume Parameter**: The `ClaudeCodeOptions.Resume` parameter is managed automatically via `AgentSession` - do not set it manually
- **Thread Reuse**: Always pass the same thread object to maintain conversation context across multiple turns
- **API Key**: Set via `ApiKey` property or `ANTHROPIC_AUTH_TOKEN` environment variable
- **Tool Permissions**: Use `PermissionMode.Auto` for automatic tool approval, or `PermissionMode.Prompt` for manual approval

## Troubleshooting

### "Claude Code CLI not found"
Ensure Claude Code CLI is installed:
```bash
npm install -g @anthropic-ai/claude-code
```

### Authentication Errors
Set your API key:
```bash
export ANTHROPIC_AUTH_TOKEN="your-api-key"
```

Or pass it via options:
```csharp
var options = new ClaudeCodeAIAgentOptions { ApiKey = "your-api-key" };
```

### Session Not Persisting
Ensure you're passing the same thread object to all `RunAsync()` calls:
```csharp
var thread = agent.GetNewThread();
await agent.RunAsync([...], thread: thread); // First turn
await agent.RunAsync([...], thread: thread); // Second turn uses same thread
```

## License

See the main ClaudeCodeSdk repository for license information.
