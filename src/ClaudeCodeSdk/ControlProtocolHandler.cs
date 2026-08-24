using System.Collections.Concurrent;
using System.Text.Json;
using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Microsoft.Extensions.Logging;

namespace ClaudeCodeSdk;

internal sealed class ControlProtocolHandler
{
    private readonly CanUseToolCallback? _canUseTool;
    private readonly Func<string, CancellationToken, Task> _writeLineAsync;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingRequests = new();

    public ControlProtocolHandler(
        CanUseToolCallback? canUseTool,
        Func<string, CancellationToken, Task> writeLineAsync,
        ILogger? logger = null
    )
    {
        _canUseTool = canUseTool;
        _writeLineAsync = writeLineAsync;
        _logger = logger;
    }

    public bool TryHandle(string line, CancellationToken cancellationToken)
    {
        JsonElement message;
        try
        {
            using var document = JsonDocument.Parse(line);
            message = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return false;
        }

        if (!message.TryGetProperty("type", out var typeElement))
        {
            return false;
        }

        return typeElement.GetString() switch
        {
            "control_request" => StartRequest(message, cancellationToken),
            "control_cancel_request" => CancelRequest(message),
            "control_response" => true,
            _ => false,
        };
    }

    public void CancelAll()
    {
        foreach (var cancellation in _pendingRequests.Values)
        {
            cancellation.Cancel();
        }
    }

    private bool StartRequest(JsonElement message, CancellationToken cancellationToken)
    {
        var requestId = GetRequiredString(message, "request_id");
        var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!_pendingRequests.TryAdd(requestId, requestCancellation))
        {
            requestCancellation.Dispose();
            throw new InvalidOperationException($"Control request '{requestId}' is already pending.");
        }

        _ = HandleRequestAsync(requestId, message, requestCancellation);
        return true;
    }

    private bool CancelRequest(JsonElement message)
    {
        var requestId = GetRequiredString(message, "request_id");
        if (_pendingRequests.TryGetValue(requestId, out var cancellation))
        {
            cancellation.Cancel();
        }

        return true;
    }

    private async Task HandleRequestAsync(
        string requestId,
        JsonElement message,
        CancellationTokenSource requestCancellation
    )
    {
        try
        {
            var request = message.GetProperty("request");
            var subtype = GetRequiredString(request, "subtype");
            if (!string.Equals(subtype, "can_use_tool", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported control request subtype '{subtype}'.");
            }

            if (_canUseTool == null)
            {
                throw new InvalidOperationException("CanUseTool callback is not configured.");
            }

            var toolName = GetRequiredString(request, "tool_name");
            var input = request.GetProperty("input").Clone();
            var toolUseId = GetOptionalString(request, "tool_use_id");
            var result = await _canUseTool(
                    toolName,
                    input,
                    new ToolPermissionContext(toolUseId),
                    requestCancellation.Token
                )
                .ConfigureAwait(false);
            var response = result switch
            {
                PermissionResultAllow allow => new Dictionary<string, object?>
                {
                    ["behavior"] = "allow",
                    ["updatedInput"] = allow.UpdatedInput ?? input,
                },
                PermissionResultDeny deny => new Dictionary<string, object?>
                {
                    ["behavior"] = "deny",
                    ["message"] = deny.Message,
                    ["interrupt"] = deny.Interrupt,
                },
                _ => throw new InvalidOperationException(
                    $"Unsupported permission result type '{result?.GetType().Name ?? "null"}'."
                ),
            };

            await WriteResponseAsync(requestId, response, requestCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
        {
            _logger?.LogDebug("Control request {RequestId} was cancelled.", requestId);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Control request {RequestId} failed.", requestId);
            try
            {
                await WriteErrorAsync(requestId, exception.Message, requestCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested) { }
            catch (Exception writeException)
            {
                _logger?.LogDebug(
                    writeException,
                    "Failed to write error response for control request {RequestId}.",
                    requestId
                );
            }
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
            requestCancellation.Dispose();
        }
    }

    private Task WriteResponseAsync(
        string requestId,
        IReadOnlyDictionary<string, object?> response,
        CancellationToken cancellationToken
    ) =>
        WriteAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "control_response",
                ["response"] = new Dictionary<string, object?>
                {
                    ["subtype"] = "success",
                    ["request_id"] = requestId,
                    ["response"] = response,
                },
            },
            cancellationToken
        );

    private Task WriteErrorAsync(string requestId, string error, CancellationToken cancellationToken) =>
        WriteAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "control_response",
                ["response"] = new Dictionary<string, object?>
                {
                    ["subtype"] = "error",
                    ["request_id"] = requestId,
                    ["error"] = error,
                },
            },
            cancellationToken
        );

    private Task WriteAsync(IReadOnlyDictionary<string, object?> value, CancellationToken cancellationToken) =>
        _writeLineAsync(JsonUtil.Serialize(value), cancellationToken);

    private static string GetRequiredString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidOperationException($"Control message is missing '{propertyName}'.");

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
}
