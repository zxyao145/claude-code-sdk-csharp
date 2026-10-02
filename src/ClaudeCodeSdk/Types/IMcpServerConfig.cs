using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Base interface for MCP server configurations.
/// </summary>
public interface IMcpServerConfig
{
    // Explicit so source-generated JSON metadata names it "type" without relying on
    // a naming policy: a property read through an interface reference is serialized
    // using the interface's own attributes, not the implementing type's.
    [JsonPropertyName("type")]
    string Type { get; }
}
