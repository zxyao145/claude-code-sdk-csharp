using ClaudeCodeSdk.Utils;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ClaudeCodeSdk;

/// <summary>
/// Message parser for Claude Code SDK responses.
/// </summary>
internal static class MessageParser
{
    private const string UUID = "uuid";

    /// <summary>
    /// Parse message from CLI output into typed Message objects.
    /// </summary>
    /// <param name="jsonElement">Raw message text from CLI output</param>
    /// <param name="logger">Optional logger for debugging</param>
    /// <returns>Parsed Message object</returns>
    /// <exception cref="MessageParseException">If parsing fails or message type is unrecognized</exception>

    public static IMessage? ParseMessage(string line, ILogger? logger = null)
    {
        Dictionary<string, object>? data;
        try
        {
            data = JsonUtil.Deserialize<Dictionary<string, object>>(line);
        }
        catch (JsonException ex)
        {
            logger?.LogError(ex, "JSON parse error: {Line}", line);
            throw new CLIJsonDecodeException(line, ex);
        }
        if (data == null)
        {
            return null;
        }
        //var jsonElement = JsonSerializer.SerializeToElement(line, JsonUtil.SNAKECASELOWER_OPTIONS);
        var jsonLineElement = JsonUtil.SnakeCaseSerializeToElement(data);
        return ParseMessage(jsonLineElement, logger);
    }


    private static IMessage ParseMessage(JsonElement jsonLine, ILogger? logger = null)
    {
        if (jsonLine.ValueKind != JsonValueKind.Object)
        {
            throw new MessageParseException(
                $"Invalid message data type (expected Object, got {jsonLine.ValueKind})",
                jsonLine);
        }

        if (!jsonLine.TryGetProperty("type", out JsonElement messageTypeEle))
        {
            throw new MessageParseException("Message 'type' field is not found", jsonLine);
        }

        var messageType = messageTypeEle.GetString();
        if (string.IsNullOrWhiteSpace(messageType))
        {
            throw new MessageParseException("Message 'type' field is null or empty", jsonLine);
        }

        return messageType switch
        {
            "system" => ParseSystemMessage(jsonLine),
            "assistant" => ParseAssistantMessage(jsonLine),
            "user" => ParseUserMessage(jsonLine),
            "result" => ParseResultMessage(jsonLine),
            _ => throw new MessageParseException($"Unknown message type: {messageType}", jsonLine)
        };
    }


    private static UserMessage ParseUserMessage(JsonElement msgData)
    {
        try
        {
            if (!msgData.TryGetProperty("message", out var messageElement) ||
                !messageElement.TryGetProperty("content", out var contentElement))
            {
                throw new MessageParseException("Missing required field in user message: content", msgData);
            }

            object content;

            if (contentElement.ValueKind == JsonValueKind.Array)
            {
                var contentBlocks = new List<IContentBlock>();
                foreach (var blockElement in contentElement.EnumerateArray())
                {
                    contentBlocks.Add(ParseContentBlock(blockElement, msgData));
                }
                content = contentBlocks;
            }
            else if (contentElement.ValueKind == JsonValueKind.String)
            {
                content = contentElement.GetString()!;
            }
            else
            {
                throw new MessageParseException("Invalid content type in user message", msgData);
            }

            return new UserMessage
            {
                Id = GetRequiredString(msgData, UUID),
                Content = content
            };
        }
        catch (Exception ex) when (ex is not MessageParseException)
        {
            throw new MessageParseException($"Error parsing user message: {ex.Message}", msgData);
        }
    }

    private static AssistantMessage ParseAssistantMessage(JsonElement msgData)
    {
        try
        {
            if (!msgData.TryGetProperty("message", out var messageElement))
            {
                throw new MessageParseException("Missing 'message' field in assistant message", msgData);
            }

            if (!messageElement.TryGetProperty("content", out var contentElement))
            {
                throw new MessageParseException("Missing 'content' field in assistant message", msgData);
            }

            if (!messageElement.TryGetProperty("model", out var modelElement))
            {
                throw new MessageParseException("Missing 'model' field in assistant message", msgData);
            }

            if (!msgData.TryGetProperty("session_id", out var sessionIdElement))
            {
                throw new MessageParseException("Missing 'session_id' field in assistant message", msgData);
            }

            if (msgData.TryGetProperty("error", out var errorElement))
            {
                var errorString = errorElement.GetString()!;
                if (!string.IsNullOrWhiteSpace(errorString))
                {
                    return new AssistantMessage
                    {
                        Id = GetRequiredString(msgData, UUID),
                        Content = [new ErrorContentBlock(errorString)],
                        Model = modelElement.GetString()!,
                        SessionId = sessionIdElement.GetString()!,
                    };
                }
            }


            var contentBlocks = new List<IContentBlock>();
            foreach (var blockElement in contentElement.EnumerateArray())
            {
                contentBlocks.Add(ParseContentBlock(blockElement, msgData));
            }

            return new AssistantMessage
            {
                Id = GetRequiredString(msgData, UUID),
                Content = contentBlocks,
                Model = modelElement.GetString()!,
                SessionId = sessionIdElement.GetString()!,
            };
        }
        catch (Exception ex) when (ex is not MessageParseException)
        {
            throw new MessageParseException($"Error parsing assistant message: {ex.Message}", msgData);
        }
    }

    private static SystemMessage ParseSystemMessage(JsonElement msgData)
    {
        try
        {
            if (!msgData.TryGetProperty("subtype", out var subtypeElement))
            {
                throw new MessageParseException("Missing 'subtype' field in system message", msgData);
            }

            if (!msgData.TryGetProperty("session_id", out var sessionIdElement))
            {
                throw new MessageParseException("Missing 'session_id' field in system message", msgData);
            }

            var dataDict = JsonUtil.SnakeCaseDeserialize<Dictionary<string, object>>(msgData.GetRawText());
            dataDict.Remove("subtype");

            return new SystemMessage
            {
                Id = GetRequiredString(msgData, UUID),
                Subtype = subtypeElement.GetString()!,
                SessionId = sessionIdElement.GetString()!,
                Data = dataDict
            };
        }
        catch (Exception ex) when (ex is not MessageParseException)
        {
            throw new MessageParseException($"Error parsing system message: {ex.Message}", msgData);
        }
    }

    private static ResultMessage ParseResultMessage(JsonElement msgData)
    {
        try
        {
            var result = new ResultMessage
            {
                Id = GetRequiredString(msgData, UUID),
                Subtype = GetRequiredString(msgData, "subtype"),
                DurationApiMs = GetRequiredInt32(msgData, "duration_api_ms"),
                DurationMs = GetRequiredInt32(msgData, "duration_ms"),
                IsError = GetRequiredBoolean(msgData, "is_error"),
                NumTurns = GetRequiredInt32(msgData, "num_turns"),
                SessionId = GetRequiredString(msgData, "session_id"),
                TotalCostUsd = GetOptionalDouble(msgData, "total_cost_usd"),
                Usage = GetOptional<Usage>(msgData, "usage"),
                Result = GetOptionalString(msgData, "result")
            };

            return result;
        }
        catch (Exception ex) when (ex is not MessageParseException)
        {
            throw new MessageParseException($"Error parsing result message: {ex.Message}", msgData);
        }
    }

    private static IContentBlock ParseContentBlock(JsonElement blockElement, JsonElement msgData)
    {

        if (!blockElement.TryGetProperty("type", out var typeElement))
        {
            throw new MessageParseException("Content block missing 'type' field", blockElement);
        }

        var blockType = typeElement.GetString();

        bool isError = false;
        if (blockElement.TryGetProperty("is_error", out var isErrorElement))
        {
            isError = isErrorElement.GetBoolean();
        }

        if (!isError)
        {
            return blockType switch
            {
                "text" => new TextBlock
                {
                    Text = GetRequiredString(blockElement, "text")
                },
                "thinking" => new ThinkingBlock
                {
                    Thinking = GetRequiredString(blockElement, "thinking"),
                    Signature = GetRequiredString(blockElement, "signature")
                },
                "tool_use" => new ToolUseBlock
                {
                    Id = GetRequiredString(blockElement, "id"),
                    Name = GetRequiredString(blockElement, "name"),
                    Input = GetRequiredDictionary(blockElement, "input")
                },
                "tool_result" => new ToolResultBlock
                {
                    ToolUseId = GetRequiredString(blockElement, "tool_use_id"),
                    Content = GetOptionalObject(blockElement, "content"),
                    ToolUseResult = GetOptionalDictionary(msgData, "tool_use_result") ?? new Dictionary<string, object>(),
                    IsError = GetOptionalBoolean(blockElement, "is_error")
                },
                _ => throw new MessageParseException($"Unknown content block type: {blockType}", blockElement)
            };
        }

        var errorDetails = blockElement.GetRawText();
        var error = new ErrorContentBlock($"parse {blockType} type message error")
        {
            Details = errorDetails
        };

        return error;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.GetString() ?? throw new MessageParseException($"Property '{propertyName}' is null", element);
        }
        throw new MessageParseException($"Missing required property: {propertyName}", element);
    }

    private static int GetRequiredInt32(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.GetInt32();
        }
        throw new MessageParseException($"Missing required property: {propertyName}", element);
    }

    private static bool GetRequiredBoolean(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.GetBoolean();
        }
        throw new MessageParseException($"Missing required property: {propertyName}", element);
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;
    }

    private static double? GetOptionalDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetDouble() : null;
    }

    private static bool? GetOptionalBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetBoolean() : null;
    }

    private static object? GetOptionalObject(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetRawText();
            }
            return JsonUtil.SnakeCaseDeserialize<object>(prop.GetRawText());
        }
        return null;
    }

    private static Dictionary<string, object> GetRequiredDictionary(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return JsonUtil.SnakeCaseDeserialize<Dictionary<string, object>>(prop.GetRawText())!;
        }
        throw new MessageParseException($"Missing required property: {propertyName}", element);
    }

    private static Dictionary<string, object>? GetOptionalDictionary(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
            {
                return new Dictionary<string, object>()
                {
                    { "content" , prop.GetRawText()},
                };
            }
            return JsonUtil.SnakeCaseDeserialize<Dictionary<string, object>>(prop.GetRawText())!;
        }
        return null;
    }

    private static T? GetOptional<T>(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.Deserialize<T>(JsonUtil.SNAKECASELOWER_OPTIONS);
        }
        return default(T);
    }
}