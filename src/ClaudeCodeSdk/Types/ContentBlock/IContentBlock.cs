using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Base interface for content blocks.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ErrorContentBlock), "error")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(ThinkingBlock), "thinking")]
[JsonDerivedType(typeof(ToolResultBlock), "tool_result")]
[JsonDerivedType(typeof(ToolUseBlock), "tool_use")]
public interface IContentBlock
{
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ErrorContentBlock), "error")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(ThinkingBlock), "thinking")]
[JsonDerivedType(typeof(ToolResultBlock), "tool_result")]
[JsonDerivedType(typeof(ToolUseBlock), "tool_use")]
public abstract record ContentBlockBase
{
}
