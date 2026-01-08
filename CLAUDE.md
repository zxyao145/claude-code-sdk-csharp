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

## Checkpoint 记录

**项目**: Claude Code SDK for C# | **时间**: 2026-01-08T15:25:20Z
**里程碑**: v0.10.0 架构简化重构完成 | **分支**: main

### 📊 技术状态
- **代码质量**: 优秀 (22个核心文件, -3文件)
- **架构健康**: 重构完成 - 移除过度抽象，统一核心实现
- **依赖状态**: 最新 (.NET 10.0)

### 📋 文档维护
- [x] **README.md**: 保持最新 (2026-01-05)
- [x] **配置同步**: 已同步
- [x] **CLAUDE.md**: 已更新架构说明
- [x] **API文档**: 完整

### 🎯 期间活动 (2026-01-05 → 2026-01-08, 3天)
- **提交数量**: 6个提交
- **主要变更**: 架构重构 - 简化核心层，移除不必要抽象
- **活动强度**: 高 - 重大架构优化
- **发展趋势**: ⬆️上升 - 代码质量和可维护性提升

### 🔧 本次重构详情
- **目标**: 简化过度设计的抽象层
- **实施**:
  - 创建统一的 `ClaudeProcess` 核心类（301行）
  - 移除 `ITransport` 接口（只有1个实现）
  - 移除 `InternalClient` 薄包装层（39行）
  - 移除 `Internal/` 命名空间隔离
  - `MessageParser` 移至主命名空间
- **成果**:
  - 代码量: ~1,569行 → ~1,472行 (-97行, -6.2%)
  - 文件数: 25个 → 22个 (-3文件)
  - 层级: 3层 → 2层（更清晰直接）
  - 测试: 15个测试全部通过 ✅

### 💡 建议行动
1. 考虑更新 README.md 添加架构简化说明
2. 准备发布 v0.10.1 包含架构优化
3. 继续完善 MAF 集成功能

**Git提交**: `df96656` | **健康度**: 9.0/10

---

### 历史记录

**项目**: Claude Code SDK for C# | **时间**: 2026-01-05T23:22:40+08:00
**里程碑**: v0.10.0 MAF集成开发中 | **分支**: main

### 📊 技术状态
- **代码质量**: 良好 (76个文件)
- **架构健康**: 发展中 - 新增 Microsoft Agent Framework 集成
- **依赖状态**: 最新 (.NET 10.0)

### 📋 文档维护
- [x] **README.md**: 已更新 (2025-08-26)
- [x] **配置同步**: 已同步
- [x] **API文档**: 完整 - 新增 MAF 模块文档

### 🎯 期间活动 (2025-08-27 → 2026-01-05, 131天)
- **提交数量**: 2个提交
- **主要变更**: 架构扩展 - 新增 ClaudeCodeSdk.MAF 项目
- **活动强度**: 高 - 完整实现 AIAgent 接口
- **发展趋势**: ⬆️上升 - 新增重要集成功能

### 💡 建议行动
1. 将新增的 ClaudeCodeSdk.MAF 项目提交到版本控制
2. 更新主 README.md 添加 MAF 集成说明
3. 考虑发布 v0.11.0 包含 MAF 支持

**Git提交**: `ac1eb0f` | **健康度**: 8.7/10

---

### 历史记录

**项目**: Claude Code SDK for C# | **时间**: 2025-08-26T00:00:00Z
**里程碑**: v0.0.20开发中 | **分支**: main

### 📊 技术状态
- **代码质量**: 良好 (42个文件)
- **架构健康**: 稳定
- **依赖状态**: 最新

### 📋 文档维护
- [x] **README.md**: 已更新 (2025-08-26)
- [x] **配置同步**: 已同步
- [x] **API文档**: 完整

### 🎯 期间活动 (持续优化)
- **提交数量**: 0个提交
- **主要变更**: 代码优化 (6个文件待提交)
- **活动强度**: 中等
- **发展趋势**: ➡️稳定

### 💡 建议行动
1. 提交待处理的代码优化
2. 完善单元测试覆盖
3. 考虑版本发布准备

**Git提交**: `c428056` | **健康度**: 8.4/10