using System.Text.Json.Serialization;

namespace ClaudeCodeSdk.Types;

public class Usage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("cache_creation_input_tokens")]
    public int CacheCreationInputTokens { get; set; }

    [JsonPropertyName("cache_read_input_tokens")]
    public int CacheReadInputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("server_tool_use")]
    public ServerToolUse? ServerToolUse { get; set; }

    [JsonPropertyName("service_tier")]
    public string? ServiceTier { get; set; }

    [JsonPropertyName("cache_creation")]
    public CacheCreation? CacheCreation { get; set; }
}

public class ServerToolUse
{
    [JsonPropertyName("web_search_requests")]
    public int WebSearchRequests { get; set; }

    [JsonPropertyName("web_fetch_requests")]
    public int WebFetchRequests { get; set; }
}

public class CacheCreation
{
    [JsonPropertyName("ephemeral_1h_input_tokens")]
    public int Ephemeral1hInputTokens { get; set; }

    [JsonPropertyName("ephemeral_5m_input_tokens")]
    public int Ephemeral5mInputTokens { get; set; }
}