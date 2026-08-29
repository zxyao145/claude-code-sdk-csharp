# Development Guide

This guide covers the repository layout and contributor workflow for the Claude Code SDK for .NET.

## Prerequisites

- .NET 10.0 SDK
- Claude Code CLI available on `PATH` for examples or tests that launch it. One supported installation method is npm, which requires Node.js 18+:

  ```bash
  npm install -g @anthropic-ai/claude-code
  ```

Most unit tests exercise protocol and mapping behavior without calling the live Claude service.

## Repository Layout

```text
claude-code-sdk-csharp/
├── src/
│   ├── ClaudeCodeSdk/              # Core CLI SDK
│   │   ├── ClaudeProcess.cs        # Subprocess and JSON-lines transport
│   │   ├── ClaudeSDKClient.cs      # ClaudeSdkClient interactive API
│   │   ├── ClaudeQuery.cs          # One-shot query API
│   │   ├── ControlProtocolHandler.cs
│   │   ├── MessageParser.cs
│   │   ├── Types/
│   │   └── Utils/
│   └── ClaudeCodeSdk.MAF/          # Microsoft Agent Framework adapter
│       ├── ClaudeCodeAIAgent.cs
│       ├── ClaudeCodeAgentSession.cs
│       ├── ClaudeSdkClientManager.cs
│       └── ClaudeStreaming*.cs     # Partial-message and history pipeline
├── tests/                          # xUnit v3 test project
├── examples/                       # Console examples for both packages
├── ClaudeCodeSdk.slnx
├── Directory.Build.props           # Shared target framework and package metadata
└── Directory.Packages.props        # Central package versions
```

## Build, Test, and Format

```bash
# Restore and build all projects
dotnet restore
dotnet build ClaudeCodeSdk.slnx

# Run the full test suite
dotnet test ClaudeCodeSdk.slnx

# Run one test class
dotnet test --filter "FullyQualifiedName~PartialMessageStreamingTests"

# Format C# sources
dotnet csharpier format .

# Run the examples
dotnet run --project examples/ClaudeCodeSdk.Examples.csproj
```

Pack both libraries in dependency order when diagnosing package output:

```bash
dotnet pack src/ClaudeCodeSdk/ClaudeCodeSdk.csproj -c Release
dotnet pack src/ClaudeCodeSdk.MAF/ClaudeCodeSdk.MAF.csproj -c Release
```

Tagged releases override `PackageVersion` in the publish workflow. The value in
`Directory.Build.props` is the local development default.

## Architecture

### Core SDK

`ClaudeProcess` owns the Claude Code CLI subprocess and its newline-delimited JSON protocol.
Both public interaction styles delegate to it:

1. `ClaudeQuery.QueryAsync(...)` manages a one-shot process automatically.
2. `ClaudeSdkClient` exposes connection, query, response, interrupt, and disposal lifecycle methods for interactive sessions.

`ControlProtocolHandler` handles stdio permission requests without blocking the receive loop.
`MessageParser` maps the five known top-level message types (`system`, `assistant`, `user`,
`result`, and `stream_event`) and skips unknown types for forward compatibility. Malformed known
messages still raise the SDK's parsing exceptions.

### Microsoft Agent Framework Integration

`ClaudeCodeAIAgent` adapts the core SDK to `Microsoft.Agents.AI.AIAgent`. Sessioned calls reuse a
`ClaudeSdkClient` through `ClaudeSdkClientManager`; calls without a session use `ClaudeQuery`.

With partial messages enabled, `ClaudePartialMessageMapper` emits `AgentResponseUpdate` chunks.
The streaming history pipeline persists completed Assistant batches before yielding and drains
remaining buffered updates on every exit path, including cancellation, source failure, and early
consumer disposal.

## Tests

The `tests/` project is organized by behavior:

- `TypesTests.cs`, `ExceptionsTests.cs`, and `UnknownMessageTypeTests.cs` cover the core type and parsing contracts.
- `ControlProtocolTests.cs` covers permissions, cancellation, and `AskUserQuestion` callbacks.
- `ClaudeMafPromptBuilderTests.cs`, `MafAgentMetadataTests.cs`, and `MafErrorContentTests.cs` cover MAF conversion behavior.
- `PartialMessageStreamingTests.cs` covers partial-event mapping and chat-history persistence across normal and exceptional exits.

Add or update tests for every behavior change. Prefer focused unit tests for protocol mapping and
reserve live CLI interaction for examples or explicit integration testing.

## Documentation

- `README.md` is the project-level consumer guide.
- `src/ClaudeCodeSdk/README.md` documents the core package.
- `src/ClaudeCodeSdk.MAF/README.md` documents MAF-specific behavior.
- `CHANGELOG.md` contains stable releases generated from conventional commits using `cliff.toml`;
  preview tags are intentionally excluded.

Update every affected audience when a public API or behavior changes. Keep implementation rules
in `CLAUDE.md` and release history in the changelog rather than duplicating either here.

## Contribution Checklist

1. Match the existing nullable, async, and naming conventions.
2. Add focused tests for the changed behavior.
3. Run formatting, build, and tests.
4. Update the relevant public README for consumer-visible changes.
5. Use Conventional Commit subjects so release notes can be generated correctly.
