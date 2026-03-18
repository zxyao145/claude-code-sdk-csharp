using ClaudeCodeSdk.Utils;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeCodeSdk;

/// <summary>
/// Message parser for Claude Code SDK responses.
/// </summary>
internal static class MessageParser
{
    /// <summary>
    /// Parse message from CLI output into typed Message objects.
    /// </summary>
    /// <param name="jsonElement">Raw message text from CLI output</param>
    /// <param name="logger">Optional logger for debugging</param>
    /// <returns>Parsed Message object</returns>
    /// <exception cref="MessageParseException">If parsing fails or message type is unrecognized</exception>

    public static IMessage? ParseMessage(string line, ILogger? logger = null)
    {
        try
        {
            var jsonNode = JsonNode.Parse(line);
            if (jsonNode is null)
            {
                return null;
            }

            if (jsonNode is not JsonObject jsonObject)
            {
                throw new MessageParseException(
                    $"Invalid message data type (expected Object, got {jsonNode.GetValueKind()})",
                    jsonNode);
            }

            return ParseMessage(jsonObject, logger);
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "JSON parse error: {Line}", line);
            throw new CLIJsonDecodeException(line, ex);
        }
    }


    private static IMessage ParseMessage(JsonObject jsonLine, ILogger? logger = null)
    {
        var normalizedMessage = NormalizeMessageJson(jsonLine);

        try
        {
            return normalizedMessage.Deserialize<IMessage>(JsonUtil.SNAKECASELOWER_OPTIONS)
                ?? throw new MessageParseException("Failed to deserialize message", normalizedMessage);
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "Message parse error: {MessageJson}", normalizedMessage.ToJsonString());
            throw new MessageParseException($"Error parsing message: {ex.Message}", normalizedMessage);
        }
    }

    private static JsonObject NormalizeMessageJson(JsonObject jsonLine)
    {
        var normalized = jsonLine.DeepClone().AsObject();
        var messageType = normalized["type"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(messageType))
        {
            throw new MessageParseException("Message 'type' field is null or empty", jsonLine);
        }

        if (normalized["message"] is JsonObject messagePayload)
        {
            foreach (var property in messagePayload)
            {
                if (property.Value is not null && normalized[property.Key] is null)
                {
                    normalized[property.Key] = property.Value.DeepClone();
                }
            }

            normalized.Remove("message");
        }
        if (messageType == MessageType.Assistant.Value)
        {
            var errorString = normalized["error"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(errorString))
            {
                normalized["content"] = JsonSerializer.SerializeToNode(
                    new List<IContentBlock> { new ErrorContentBlock(errorString) },
                    JsonUtil.SNAKECASELOWER_OPTIONS);
            }
        }

        if (messageType == MessageType.System.Value)
        {
            var dataNode = normalized.DeepClone().AsObject();
            dataNode.Remove("subtype");
            normalized["data"] = dataNode;
        }

        return normalized;
    }
}
