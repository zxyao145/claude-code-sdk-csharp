using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

/// <summary>
/// Result message with cost and usage information.
/// </summary>
public record ResultMessage : IMessage
{
    public string Id { get; init; } = "";

    public MessageType Type => MessageType.Result;

    [JsonPropertyName("subtype")]
    public required string Subtype { get; init; }

    [JsonPropertyName("duration_ms")]
    public required int DurationMs { get; init; }

    [JsonPropertyName("duration_api_ms")]
    public required int DurationApiMs { get; init; }

    [JsonPropertyName("is_error")]
    public required bool IsError { get; init; }

    [JsonPropertyName("num_turns")]
    public required int NumTurns { get; init; }

    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    [JsonPropertyName("total_cost_usd")]
    public double? TotalCostUsd { get; init; }

    [JsonPropertyName("usage")]
    public Usage? Usage { get; init; }

    [JsonPropertyName("result")]
    public string? Result { get; init; }

    /// <summary>
    /// Validated JSON returned by the CLI when structured output is enabled.
    /// A missing field is null; an explicit JSON null is preserved as a JsonElement.
    /// </summary>
    [JsonPropertyName("structured_output")]
    public JsonElement? StructuredOutput { get; init; }
}
