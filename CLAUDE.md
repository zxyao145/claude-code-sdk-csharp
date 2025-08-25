# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Essential Commands

### Building and Testing
```bash
# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Run examples
dotnet run --project examples/

# Pack NuGet package for release
dotnet pack src/ClaudeCodeSdk/ClaudeCodeSdk.csproj -c Release
```

### Prerequisites for Development
- .NET 8.0 SDK
- Node.js (required for Claude Code CLI)
- Claude Code CLI: `npm install -g @anthropic-ai/claude-code`

## Architecture Overview

### Core SDK Structure
The SDK implements a dual-pattern architecture for Claude Code interactions:

1. **One-shot Queries** (`ClaudeQuery.QueryAsync`)
   - Fire-and-forget pattern for simple queries
   - Streams responses as `IAsyncEnumerable<IMessage>`
   - Automatically handles connection lifecycle

2. **Interactive Client** (`ClaudeSDKClient`)
   - Long-lived bidirectional communication
   - Session management and interrupt support  
   - Manual connection control via `ConnectAsync/DisconnectAsync`

### Transport Layer
- `SubprocessCliTransport` handles subprocess communication with Claude Code CLI
- JSON-based message protocol with strongly-typed parsing via `MessageParser`
- Automatic CLI discovery and process lifecycle management

### Message System
Hierarchical message types implementing `IMessage`:
- `AssistantMessage` - Claude's responses with content blocks
- `UserMessage` - User input
- `SystemMessage` - System notifications and metadata
- `ResultMessage` - End-of-conversation marker with cost/usage data

Content blocks implement `IContentBlock`:
- `TextBlock` - Plain text content
- `ToolUseBlock` - Tool invocations  
- `ToolResultBlock` - Tool execution results
- `ThinkingBlock` - Claude's reasoning (when enabled)

### Exception Hierarchy
Custom exceptions inherit from `ClaudeSDKException`:
- `CLINotFoundException` - Claude Code CLI not found
- `CLIConnectionException` - Transport connection issues
- `ProcessException` - Subprocess execution failures
- `CLIJsonDecodeException` - Message parsing errors
- `MessageParseException` - Type conversion failures

## Key Implementation Details

### ReceiveMessagesAsync Termination
The transport layer automatically terminates message streaming when receiving a `"result"` type message, preventing infinite waiting after conversation completion.

### JSON Serialization
Uses consistent `JsonSerializerOptions` with `snake_case_lower` naming policy throughout the codebase for Claude Code CLI compatibility.

### Resource Management
Implements proper `IAsyncDisposable` patterns for subprocess cleanup and connection management.

### Environment Variables
Supports authentication and configuration through environment variables:
- `ANTHROPIC_AUTH_TOKEN` - API authentication
- `ANTHROPIC_BASE_URL` - Custom API endpoint
- `CLAUDE_CODE_ENTRYPOINT` - SDK identifier (automatically set to "sdk-csharp")

## Testing Strategy

Tests are organized by component in `tests/` folder:
- Type system validation
- Exception handling verification  
- Message parsing correctness
- Client interaction patterns

Examples in `examples/` demonstrate real-world usage patterns including tool integration and streaming scenarios.