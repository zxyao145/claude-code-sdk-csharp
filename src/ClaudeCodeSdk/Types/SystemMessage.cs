using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// System message with metadata.
/// </summary>
public record SystemMessage : IMessage
{
    public string Type => "system";

    [JsonPropertyName("subtype")]
    public required string Subtype { get; init; }

    [JsonPropertyName("data")]
    public required Dictionary<string, object> Data { get; init; }
}