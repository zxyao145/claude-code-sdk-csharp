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
dotnet run --project examples/ClaudeCodeSdk.Examples.csproj

# Pack NuGet packages for release
dotnet pack src/ClaudeCodeSdk/ClaudeCodeSdk.csproj -c Release
dotnet pack src/ClaudeCodeSdk.MAF/ClaudeCodeSdk.MAF.csproj -c Release
```

### Formatting
```bash
# Format all code with CSharpier
dotnet csharpier format .
```

### Prerequisites for Development
- .NET 10.0 SDK
- Claude Code CLI available on `PATH`
- Node.js 18+ only when installing the CLI through npm: `npm install -g @anthropic-ai/claude-code`

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
- **`ClaudeProcess`** - Single unified process manager
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
- Handles five known message types: `assistant`, `user`, `system`, `result`, `stream_event`
- Skips unknown top-level message types for forward compatibility; malformed known messages still fail parsing
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

Tests are organized by behavior in `tests/`:
- Core protocol: types, exceptions, unknown-message handling, and stdio control requests
- MAF integration: prompt mapping, metadata, error content, partial streaming, and history persistence

Examples in `examples/` demonstrate real-world usage patterns including tool integration and streaming scenarios.

## Microsoft Agent Framework (MAF) Integration

### Structure (`src/ClaudeCodeSdk.MAF/`)
- `ClaudeCodeAIAgent` - Main AIAgent implementation
- `ClaudeCodeAgentSession` - Session management with session ID persistence
- `ClaudeCodeAIAgentOptions` - Configuration wrapper for MAF-specific settings
- `ClaudeSdkClientManager` - Manages ClaudeSdkClient lifecycle across sessions
- `ClaudePartialMessageMapper` - Converts raw partial events into MAF response updates
- `ClaudeStreamingMessageProcessor` / `ClaudeStreamingHistoryAccumulator` - Coordinate streaming and history batches

### Key Behaviors
- Prompt construction uses the first `ChatRole.User` message and forwards its text and image `DataContent`
- Per-request `ChatRole.System` messages are ignored; configure `ClaudeCodeAIAgentOptions.SystemPrompt` or `AppendSystemPrompt`
- Agent session IDs are sent as `session_id` on user messages for conversation continuity
- `RunAsync()` returns an `AgentResponse` after collecting messages through `ResultMessage`
- `RunStreamingAsync()` yields `AgentResponseUpdate` chunks; partial mode emits text and thinking deltas immediately and complete tool calls at block end
- Content blocks are converted to MAF types: `TextContent`, `FunctionCallContent`, `FunctionResultContent`, `TextReasoningContent`, `ErrorContent`

### Important Notes
- `ClaudeCodeOptions.Resume` is not used by the MAF adapter; pass an `AgentSession` for continuity
- `ClaudeSdkClientManager` automatically handles client creation/disposal when switching between sessions
- Session state persists via the session's `SessionId` which maps to Claude Code sessions
- When a different `AgentSession` is used, the manager automatically disposes the old client and creates a new one
- With `ChatHistoryProvider`, persist completed Assistant batches before yielding and drain any remainder on every exit path; keep the stream exception primary if final persistence also fails

## Project Structure

```
src/
├── ClaudeCodeSdk/              # Core SDK package
│   ├── ClaudeProcess.cs        # Unified subprocess manager
│   ├── ClaudeSDKClient.cs      # ClaudeSdkClient interactive API
│   ├── ClaudeQuery.cs          # One-shot query API
│   ├── ControlProtocolHandler.cs # Stdio permission and user-input callbacks
│   ├── MessageParser.cs        # JSON-to-type conversion
│   ├── Types/                  # Messages, content blocks, options, permissions
│   ├── Utils/
│   │   ├── JsonUtil.cs         # snake_case serialization
│   │   └── CommandUtil.cs      # CLI argument builder
│   └── Exceptions/
│       └── ClaudeSDKExceptions.cs
│
├── ClaudeCodeSdk.MAF/          # Microsoft Agent Framework integration
│   ├── ClaudeCodeAIAgent.cs    # AIAgent implementation
│   ├── ClaudeCodeAgentSession.cs
│   ├── ClaudeCodeAIAgentOptions.cs
│   ├── ClaudeSdkClientManager.cs
│   └── ClaudeStreaming*.cs     # Partial-message persistence pipeline
│
examples/                       # Usage examples
tests/                          # Unit tests
```

## Documentation Map

- `README.md` - Project overview and common consumer workflows
- `src/ClaudeCodeSdk/README.md` - Core SDK concepts and API details
- `src/ClaudeCodeSdk.MAF/README.md` - MAF integration, streaming, and history persistence
- `DEVELOPMENT.md` - Contributor workflow and repository structure
- `CHANGELOG.md` - Stable releases generated from conventional commits via `cliff.toml`
