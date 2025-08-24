# Changelog

All notable changes to the Claude Code SDK for .NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.20] - 2024-12-23

### Added
- Initial release of Claude Code SDK for .NET
- Core SDK functionality translated from Python version
- `ClaudeQuery.QueryAsync()` for one-shot queries
- `ClaudeSDKClient` for interactive, bidirectional conversations
- Full type system with strongly-typed messages and content blocks
- Subprocess CLI transport implementation
- Comprehensive error handling with custom exception types
- Message parsing with JSON support
- Support for all Claude Code options and tools
- Examples demonstrating basic usage, streaming, and interactive scenarios
- Unit tests for core functionality
- Complete documentation and README

### Features
- **Bidirectional Communication**: Full support for interactive conversations
- **Tool Integration**: Complete tool system with permission modes
- **Streaming Support**: Real-time message streaming
- **Type Safety**: Strongly-typed messages and content blocks
- **Error Handling**: Comprehensive exception system
- **Async/Await**: Full async support throughout the API
- **Cross-Platform**: Compatible with all .NET 8.0+ platforms
- **Cancellation Support**: CancellationToken support for all async operations

### Supported Message Types
- `UserMessage` - User input messages
- `AssistantMessage` - Claude's responses with content blocks
- `SystemMessage` - System-level messages
- `ResultMessage` - Conversation results with cost and usage info

### Supported Content Blocks
- `TextBlock` - Plain text content
- `ThinkingBlock` - Claude's reasoning process
- `ToolUseBlock` - Tool execution requests
- `ToolResultBlock` - Tool execution results

### Configuration Options
- System prompts and prompt appending
- Tool allowlists and denylists
- Permission modes (default, acceptEdits, plan, bypassPermissions)
- Working directory and additional directories
- MCP server configurations
- Model selection and thinking token limits
- Session management and conversation continuation