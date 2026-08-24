using System.Text.Json;

namespace ClaudeCodeSdk.Types;

public delegate ValueTask<PermissionResult> CanUseToolCallback(
    string toolName,
    JsonElement input,
    ToolPermissionContext context,
    CancellationToken cancellationToken
);

public sealed record ToolPermissionContext(string? ToolUseId);

public abstract record PermissionResult;

public sealed record PermissionResultAllow(JsonElement? UpdatedInput = null) : PermissionResult;

public sealed record PermissionResultDeny(string Message, bool Interrupt = false) : PermissionResult;
