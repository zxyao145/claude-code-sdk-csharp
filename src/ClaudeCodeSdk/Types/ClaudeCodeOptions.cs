namespace ClaudeCodeSdk.Types;

/// <summary>
/// Query options for Claude SDK.
/// </summary>
public record ClaudeCodeOptions
{
    public IReadOnlyList<string> AllowedTools { get; init; } = [];
    public int MaxThinkingTokens { get; init; } = 8000;
    public string? SystemPrompt { get; init; }
    public string? AppendSystemPrompt { get; init; }
    public IReadOnlyDictionary<string, IMcpServerConfig> McpServers { get; init; } = new Dictionary<string, IMcpServerConfig>();
    public string? McpServersPath { get; init; }
    public PermissionMode? PermissionMode { get; init; }
    public bool ContinueConversation { get; init; } = false;
    public string? Resume { get; init; }
    public Guid? SessionId { get; init; }
    public int? MaxTurns { get; init; }
    public IReadOnlyList<string> DisallowedTools { get; init; } = [];
    public string? Model { get; init; }
    public string? PermissionPromptToolName { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? Settings { get; init; }
    public IReadOnlyList<string> AddDirectories { get; init; } = [];
    public IReadOnlyDictionary<string, string?> ExtraArgs { get; init; } = new Dictionary<string, string?>();



    public List<string>? AddDirs { get; set; }


    /// <summary>
    /// ANTHROPIC_AUTH_TOKEN, and it will override the value in EnvironmentVariables if set.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// ANTHROPIC_BASE_URL, and it will override the value in EnvironmentVariables if set.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    public Dictionary<string, string?>? EnvironmentVariables { get; set; }
}