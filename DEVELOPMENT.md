# Development Guide

This document provides information for developers working on the Claude Code SDK for .NET.

## Project Structure

```
claude-code-sdk-csharp/
├── src/ClaudeCodeSdk/              # Main SDK library
│   ├── Types/                      # Type definitions and models
│   ├── Exceptions/                 # Custom exception types
│   ├── Internal/                   # Internal implementation
│   │   └── Transport/              # Transport layer
│   ├── ClaudeSDKClient.cs         # Interactive client
│   ├── ClaudeQuery.cs             # One-shot query functionality
│   └── ClaudeCodeSDK.cs           # Main SDK entry point
├── examples/                       # Usage examples
├── tests/                         # Unit tests
├── ClaudeCodeSdk.sln              # Solution file
└── README.md                      # Documentation
```

## Building

```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Pack NuGet package
dotnet pack src/ClaudeCodeSdk/ClaudeCodeSdk.csproj -c Release
```

## Code Style

- Follow standard C# naming conventions
- Use nullable reference types throughout
- Prefer records for data types
- Use async/await for all I/O operations
- Include XML documentation for public APIs

## Key Components

### Transport Layer

The transport layer handles communication with the Claude CLI subprocess:

- `ITransport` - Interface for transport implementations
- `SubprocessCliTransport` - Default subprocess implementation

### Message System

Messages are parsed from JSON into strongly-typed objects:

- `MessageParser` - Converts JSON to typed messages
- Message types implement `IMessage`
- Content blocks implement `IContentBlock`

### Client Architecture

Two main interaction patterns:

1. **One-shot queries** via `ClaudeQuery.QueryAsync()`
   - Simple, fire-and-forget style
   - Good for batch processing
   
2. **Interactive conversations** via `ClaudeSDKClient`
   - Bidirectional communication
   - Session management
   - Interrupt support

## Testing

Tests are organized by component:

- `TypesTests.cs` - Type system tests
- `ExceptionsTests.cs` - Exception handling tests
- `MessageParserTests.cs` - Message parsing tests
- `ClientTests.cs` - Client functionality tests

Run tests with:
```bash
dotnet test --verbosity normal
```

## Examples

The examples project demonstrates:

- Basic query usage
- Advanced options
- Tool integration  
- Interactive conversations
- Streaming scenarios

Run examples with:
```bash
dotnet run --project examples/
```

## Error Handling

The SDK uses a hierarchy of custom exceptions:

```
ClaudeSDKException (base)
├── CLIConnectionException
│   └── CLINotFoundException
├── ProcessException
├── CLIJsonDecodeException
└── MessageParseException
```

## Performance Considerations

- Use `IAsyncEnumerable` for streaming to avoid buffering all messages
- Implement proper disposal patterns for resources
- Cancel long-running operations with `CancellationToken`
- Pool JSON serializer options to reduce allocations

## Contributing

1. Follow the existing code style and patterns
2. Add tests for new functionality
3. Update documentation for public API changes
4. Ensure compatibility with .NET 8.0+