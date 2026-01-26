using ClaudeCodeSdk.Types;

namespace ClaudeCodeSdk.MAF;

public class ClaudeCodeAIAgentOptions
{
    public IReadOnlyList<string> AllowedTools { get; init; } = [];
    public int MaxThinkingTokens { get; init; } = 8000;
    public string? SystemPrompt { get; init; }
    public string? AppendSystemPrompt { get; init; }
    public IReadOnlyDictionary<string, IMcpServerConfig> McpServers { get; init; } = new Dictionary<string, IMcpServerConfig>();
    public string? McpServersPath { get; init; }
    public PermissionMode? PermissionMode { get; init; }
    public bool ContinueConversation { get; init; } = false;
    //public string? Resume { get; init; }
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


    public static ClaudeCodeAIAgentOptions? From(ClaudeCodeOptions? claudeCodeOptions)
    {
        if (claudeCodeOptions == null)
        { 
            return null; 
        }

        var claudeCodeAIAgentOptions = new ClaudeCodeAIAgentOptions
        {
            AllowedTools = claudeCodeOptions.AllowedTools,
            MaxThinkingTokens = claudeCodeOptions.MaxThinkingTokens,
            SystemPrompt = claudeCodeOptions.SystemPrompt,
            AppendSystemPrompt = claudeCodeOptions.AppendSystemPrompt,
            McpServers = claudeCodeOptions.McpServers,
            McpServersPath = claudeCodeOptions.McpServersPath,
            PermissionMode = claudeCodeOptions.PermissionMode,
            ContinueConversation = claudeCodeOptions.ContinueConversation,
            SessionId = claudeCodeOptions.SessionId,
            MaxTurns = claudeCodeOptions.MaxTurns,
            DisallowedTools = claudeCodeOptions.DisallowedTools,
            Model = claudeCodeOptions.Model,
            PermissionPromptToolName = claudeCodeOptions.PermissionPromptToolName,
            WorkingDirectory = claudeCodeOptions.WorkingDirectory,
            Settings = claudeCodeOptions.Settings,
            AddDirectories = claudeCodeOptions.AddDirectories,
            ExtraArgs = claudeCodeOptions.ExtraArgs,

            AddDirs = claudeCodeOptions.AddDirs,

            ApiKey = claudeCodeOptions.ApiKey,
            BaseUrl = claudeCodeOptions.BaseUrl,
            EnvironmentVariables = claudeCodeOptions.EnvironmentVariables
        };

        return claudeCodeAIAgentOptions;
    }

    public ClaudeCodeOptions ToClaudeCodeOptions()
    {
        var source = this;

        var options = new ClaudeCodeOptions
        {
            AllowedTools = source.AllowedTools,
            MaxThinkingTokens = source.MaxThinkingTokens,
            SystemPrompt = source.SystemPrompt,
            AppendSystemPrompt = source.AppendSystemPrompt,
            McpServers = source.McpServers,
            McpServersPath = source.McpServersPath,
            PermissionMode = source.PermissionMode,
            ContinueConversation = source.ContinueConversation,
            SessionId = source.SessionId,
            MaxTurns = source.MaxTurns,
            DisallowedTools = source.DisallowedTools,
            Model = source.Model,
            PermissionPromptToolName = source.PermissionPromptToolName,
            WorkingDirectory = source.WorkingDirectory,
            Settings = source.Settings,
            AddDirectories = source.AddDirectories,
            ExtraArgs = source.ExtraArgs,

            AddDirs = source.AddDirs,

            ApiKey = source.ApiKey,
            BaseUrl = source.BaseUrl,
            EnvironmentVariables = source.EnvironmentVariables,

        };

        return options;
    }
}