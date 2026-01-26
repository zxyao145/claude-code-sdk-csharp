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

# Run a specific test
dotnet test --filter "FullyQualifiedName~ExceptionsTests"

# Run examples
dotnet run --project examples/

# Pack NuGet packages for release
dotnet pack src/ClaudeCodeSdk/ClaudeCodeSdk.csproj -c Release
dotnet pack src/ClaudeCodeSdk.MAF/ClaudeCodeSdk.MAF.csproj -c Release
```

### Prerequisites for Development
- .NET 10.0 SDK (supports .NET 8.0+ for compatibility)
- Node.js (required for Claude Code CLI)
- Claude Code CLI: `npm install -g @anthropic-ai/claude-code`

## Architecture Overview

### Core SDK Structure
The SDK implements a simplified dual-pattern architecture for Claude Code interactions:

1. **One-shot Queries** (`ClaudeQuery.QueryAsync`)
   - Fire-and-forget pattern for simple queries
   - Streams responses as `IAsyncEnumerable<IMessage>`
   - Automatically handles connection lifecycle

2. **Interactive Client** (`ClaudeSdkClient`)
   - Long-lived bidirectional communication
   - Session management and interrupt support
   - Manual connection control via `ConnectAsync/DisconnectAsync`

### Unified Core Layer
- **`ClaudeProcess`** - Single unified process manager (replaces ITransport abstraction)
  - Direct subprocess communication with Claude Code CLI
  - JSON-based message protocol with strongly-typed parsing
  - Automatic CLI discovery and process lifecycle management
  - Shared by both ClaudeQuery and ClaudeSdkClient

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

### Message Streaming and Termination
- `ClaudeProcess.ReceiveAsync()` automatically terminates when receiving a `ResultMessage` (type="result")
- Both `ClaudeQuery` and `ClaudeSdkClient` rely on this automatic termination
- `ClaudeSdkClient.ReceiveResponseAsync()` provides convenience method that yields until ResultMessage

### Message Parsing (`MessageParser`)
- Converts JSON from CLI stdout into strongly-typed `IMessage` objects
- Handles four message types: `assistant`, `user`, `system`, `result`
- Content blocks are polymorphic (`TextBlock`, `ToolUseBlock`, `ToolResultBlock`, `ThinkingBlock`, `ErrorContentBlock`)
- Throws `MessageParseException` or `CLIJsonDecodeException` on invalid input

### JSON Serialization
- Uses `snake_case_lower` naming policy via `JsonUtil` for Claude Code CLI compatibility
- Consistent serialization across all message exchanges
- All option properties and message fields follow this convention

### Resource Management
- All process-managing classes implement `IAsyncDisposable`
- `ClaudeProcess` handles subprocess lifecycle (start, kill, cleanup)
- Automatically cleans up stdin/stdout streams and process handles
- Use `await using` for automatic cleanup

### Environment Variables
Configuration through environment variables (automatically set by SDK):
- `ANTHROPIC_AUTH_TOKEN` - API authentication (from `ClaudeCodeOptions.ApiKey`)
- `ANTHROPIC_BASE_URL` - Custom API endpoint (from `ClaudeCodeOptions.BaseUrl`)
- `CLAUDE_CODE_ENTRYPOINT` - SDK identifier (always "sdk-csharp")

## Testing Strategy

Tests are organized by component in `tests/` folder:
- `TypesTests.cs` - Type system validation and message parsing
- `ExceptionsTests.cs` - Exception handling verification

Examples in `examples/` demonstrate real-world usage patterns including tool integration and streaming scenarios.

## Microsoft Agent Framework (MAF) Integration

### Structure (`src/ClaudeCodeSdk.MAF/`)
- `ClaudeCodeAIAgent` - Main AIAgent implementation
- `ClaudeCodeAgentThread` - Thread management with session ID persistence
- `ClaudeCodeAIAgentOptions` - Configuration wrapper for MAF-specific settings
- `ClaudeSdkClientManager` - Manages ClaudeSdkClient lifecycle across sessions

### Key Behaviors
- System messages are extracted and set as `SystemPrompt` in `ClaudeCodeOptions`
- Thread session IDs map to Claude Code's `Resume` parameter for conversation continuity
- `RunAsync()` returns complete response after collecting all messages until `ResultMessage`
- `RunStreamingAsync()` yields `AgentRunResponseUpdate` for each message received
- Content blocks are converted to MAF types: `TextContent`, `FunctionCallContent`, `FunctionResultContent`, `TextReasoningContent`, `ErrorContent`

### Important Notes
- `ClaudeCodeOptions.Resume` is managed automatically via `AgentThread` - do not set manually
- `ClaudeSdkClientManager` automatically handles client creation/disposal when switching between threads
- Session state persists via the thread's `SessionId` which maps to Claude Code sessions
- When a different `AgentThread` (with different `SessionId`) is used, the manager automatically disposes the old client and creates a new one

## Project Structure

```
src/
├── ClaudeCodeSdk/              # Core SDK package
│   ├── ClaudeProcess.cs        # Unified subprocess manager (301 lines)
│   ├── ClaudeSdkClient.cs      # Interactive client with manual lifecycle
│   ├── ClaudeQuery.cs          # One-shot query API
│   ├── MessageParser.cs        # JSON-to-type conversion
│   ├── Types/                  # Message and configuration types
│   │   ├── IMessage.cs         # Base message interface
│   │   ├── IContentBlock.cs    # Content block interface
│   │   ├── AssistantMessage.cs
│   │   ├── UserMessage.cs
│   │   ├── SystemMessage.cs
│   │   ├── ResultMessage.cs
│   │   ├── TextBlock.cs
│   │   ├── ThinkingBlock.cs
│   │   ├── ToolUseBlock.cs
│   │   ├── ToolResultBlock.cs
│   │   ├── ErrorContentBlock.cs
│   │   ├── ClaudeCodeOptions.cs
│   │   └── Usage.cs
│   ├── Utils/
│   │   ├── JsonUtil.cs         # snake_case serialization
│   │   └── CommandUtil.cs      # CLI argument builder
│   └── Exceptions/
│       └── ClaudeSDKExceptions.cs
│
├── ClaudeCodeSdk.MAF/          # Microsoft Agent Framework integration
│   ├── ClaudeCodeAIAgent.cs    # AIAgent implementation
│   ├── ClaudeCodeAgentThread.cs
│   ├── ClaudeCodeAIAgentOptions.cs
│   └── ClaudeSdkClientManager.cs  # Client lifecycle manager
│
examples/                       # Usage examples
tests/                          # Unit tests
```
