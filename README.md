# Claude Code SDK for .NET

.NET SDK for Claude Code. See the [Claude Code SDK documentation](https://docs.anthropic.com/en/docs/claude-code/sdk) for more information.

## Installation

```bash
dotnet add package ClaudeCodeSdk
```

**Prerequisites:**
- .NET 8.0 or later
- Node.js 
- Claude Code: `npm install -g @anthropic-ai/claude-code`

## Quick Start

```csharp
using ClaudeCodeSdk;

// Simple query
await foreach (var message in ClaudeQuery.QueryAsync("What is 2 + 2?"))
{
    if (message is AssistantMessage assistantMsg)
    {
        foreach (var block in assistantMsg.Content)
        {
            if (block is TextBlock textBlock)
            {
                Console.WriteLine(textBlock.Text);
            }
        }
    }
}
```

## Usage

### Basic Query

```csharp
using ClaudeCodeSdk;

// Simple query
await foreach (var message in ClaudeQuery.QueryAsync("Hello Claude"))
{
    if (message is AssistantMessage assistantMessage)
    {
        foreach (var block in assistantMessage.Content)
        {
            if (block is TextBlock textBlock)
            {
                Console.WriteLine(textBlock.Text);
            }
        }
    }
}

// With options
var options = new ClaudeCodeOptions
{
    SystemPrompt = "You are a helpful assistant",
    MaxTurns = 1
};

await foreach (var message in ClaudeQuery.QueryAsync("Tell me a joke", options))
{
    Console.WriteLine(message);
}
```

### Using Tools

```csharp
var options = new ClaudeCodeOptions
{
    AllowedTools = new[] { "Read", "Write", "Bash" },
    PermissionMode = PermissionMode.AcceptEdits // auto-accept file edits
};

await foreach (var message in ClaudeQuery.QueryAsync(
    "Create a hello.cs file", 
    options))
{
    // Process tool use and results
}
```

### Working Directory

```csharp
var options = new ClaudeCodeOptions
{
    WorkingDirectory = "/path/to/project"
};
```

### Interactive Client

```csharp
// For bidirectional, interactive conversations
await using var client = new ClaudeSDKClient();
await client.ConnectAsync();

// Send initial message
await client.QueryAsync("Let's solve a math problem");

// Receive response
await foreach (var message in client.ReceiveResponseAsync())
{
    if (message is AssistantMessage assistantMessage)
    {
        // Process response
    }
    if (message is ResultMessage)
        break; // Response complete
}

// Send follow-up
await client.QueryAsync("What's 15% of 80?");
```

## API Reference

### `ClaudeQuery.QueryAsync(prompt, options)`

Main static method for querying Claude.

**Parameters:**
- `prompt` (string): The prompt to send to Claude
- `options` (ClaudeCodeOptions): Optional configuration
- `cancellationToken` (CancellationToken): Optional cancellation token

**Returns:** IAsyncEnumerable<IMessage> - Stream of response messages

### `ClaudeSDKClient`

Client class for interactive conversations with full control over the conversation flow.

**Key Methods:**
- `ConnectAsync()` - Connect to Claude
- `QueryAsync(prompt)` - Send a message
- `ReceiveMessagesAsync()` - Receive all messages 
- `ReceiveResponseAsync()` - Receive until ResultMessage
- `InterruptAsync()` - Interrupt current operation
- `DisconnectAsync()` - Disconnect

### Types

Key types include:
- `ClaudeCodeOptions` - Configuration options
- `AssistantMessage`, `UserMessage`, `SystemMessage`, `ResultMessage` - Message types
- `TextBlock`, `ToolUseBlock`, `ToolResultBlock`, `ThinkingBlock` - Content blocks
- `PermissionMode` - Tool execution permissions

## Error Handling

```csharp
using ClaudeCodeSdk.Exceptions;

try
{
    await foreach (var message in ClaudeQuery.QueryAsync("Hello"))
    {
        // Process messages
    }
}
catch (CLINotFoundException)
{
    Console.WriteLine("Please install Claude Code");
}
catch (ProcessException ex)
{
    Console.WriteLine($"Process failed with exit code: {ex.ExitCode}");
}
catch (CLIJsonDecodeException ex)
{
    Console.WriteLine($"Failed to parse response: {ex}");
}
```

## Available Tools

See the [Claude Code documentation](https://docs.anthropic.com/en/docs/claude-code/settings#tools-available-to-claude) for a complete list of available tools.

## Examples

See the `examples/` folder for complete working examples including:
- Basic queries
- Interactive conversations  
- Tool usage
- Streaming mode

## License

MIT