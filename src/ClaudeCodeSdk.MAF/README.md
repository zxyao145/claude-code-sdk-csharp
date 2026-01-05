# ClaudeCodeSdk.MAF

Microsoft Agent Framework (MAF) integration for ClaudeCodeSdk, providing an `AIAgent` implementation for Claude Code interactions.

## Overview

This package integrates ClaudeCodeSdk with Microsoft Agent Framework, allowing you to use Claude Code as an AIAgent with full support for:

- ✅ Streaming and non-streaming responses
- ✅ Multi-turn conversations with thread management
- ✅ Thread serialization/deserialization for persistence
- ✅ Compatible with Microsoft.Extensions.AI interfaces

## Installation

```bash
dotnet add package ClaudeCodeSdk.MAF
```

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
using ClaudeCodeSdk.Types;

var agent = new ClaudeCodeAIAgent();

await foreach (var update in agent.RunStreamingAsync("Explain async/await in C#"))
{
    Console.Write(update.Text);
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
using ClaudeCodeSdk.Types;
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

// Second turn - context is preserved
var response2 = await agent.RunAsync(
    [new ChatMessage(ChatRole.User, "Can you show me an example in C#?")],
    thread: thread
);
Console.WriteLine($"Claude: {response2.Text}");
```

### Advanced Configuration

```csharp
using ClaudeCodeSdk.MAF;
using ClaudeCodeSdk.Types;
using Microsoft.Extensions.Logging;

// Configure Claude Code options
var options = new ClaudeCodeOptions
{
    MaxThinkingTokens = 10000,
    SystemPrompt = "You are an expert C# developer.",
    Model = "claude-sonnet-4",
    PermissionMode = PermissionMode.Auto
};

// Optional: Add logging
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ClaudeCodeAIAgent>();

var agent = new ClaudeCodeAIAgent(options, logger);

var response = await agent.RunAsync("Help me optimize this LINQ query...");
Console.WriteLine(response.Text);
```

### Thread Persistence

```csharp
using System.Text.Json;
using ClaudeCodeSdk.MAF;

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
- Maintains conversation history
- Manages session IDs for Claude Code
- Supports serialization for persistence

## Requirements

- .NET 10.0 or later
- Claude Code CLI installed (`npm install -g @anthropic-ai/claude-code`)
- ANTHROPIC_AUTH_TOKEN environment variable set

## Configuration Options

All `ClaudeCodeOptions` from the base SDK are supported:

| Option | Description |
|--------|-------------|
| `MaxThinkingTokens` | Maximum tokens for Claude's reasoning (default: 8000) |
| `SystemPrompt` | Custom system prompt |
| `Model` | Claude model to use (e.g., "claude-sonnet-4") |
| `PermissionMode` | Tool permission mode (Auto, Prompt, Deny) |
| `AllowedTools` | List of allowed tools |
| `DisallowedTools` | List of disallowed tools |
| `McpServers` | MCP server configurations |

## License

See the main ClaudeCodeSdk repository for license information.
