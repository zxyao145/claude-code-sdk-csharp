using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeCodeSdk.Types;

namespace ClaudeCodeSdk.Utils;

/// <summary>
/// Source-generated JSON metadata for the shapes the SDK reads and writes: stdin
/// messages, control protocol responses, --mcp-config, and CLI output parsing.
/// Lets <see cref="JsonUtil"/> work with reflection disabled (e.g. native AOT).
/// </summary>
// Container and boxed-value shapes used by the Dictionary<string, object>-based
// stdin/control-protocol messages (see ClaudeProcess, ControlProtocolHandler,
// ClaudeMafPromptBuilder, and MessageParser).
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, object>))]
[JsonSerializable(typeof(List<Dictionary<string, object>>))]
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(ReadOnlyMemory<byte>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
// --mcp-config (CommandUtil) serializes options.McpServers through IMcpServerConfig.
[JsonSerializable(typeof(IMcpServerConfig))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, IMcpServerConfig>))]
[JsonSerializable(typeof(McpHttpServerConfig))]
[JsonSerializable(typeof(McpSSEServerConfig))]
[JsonSerializable(typeof(McpStdioServerConfig))]
// Usage is deserialized off a result message (MessageParser.GetOptional<Usage>).
[JsonSerializable(typeof(Usage))]
[JsonSerializable(typeof(ServerToolUse))]
[JsonSerializable(typeof(CacheCreation))]
[ExcludeFromCodeCoverage]
internal partial class SdkJsonContext : JsonSerializerContext;
