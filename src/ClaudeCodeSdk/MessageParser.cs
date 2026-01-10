using ClaudeCodeSdk.Utils;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ClaudeCodeSdk;

/// <summary>
/// Message parser for Claude Code SDK responses.
/// </summary>
internal static class MessageParser
{
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
        var jsonElement = JsonUtil.SnakeCaseSerializeToElement(data);

        return ParseMessage(jsonElement, logger);
    }


    /// <summary>
    /// Parse message from CLI output into typed Message objects.
    /// </summary>
    /// <param name="jsonElement">Raw message dictionary from CLI output</param>
    /// <param name="logger">Optional logger for debugging</param>
    /// <returns>Parsed Message object</returns>
    /// <exception cref="MessageParseException">If parsing fails or message type is unrecognized</exception>
    public static IMessage ParseMessage(JsonElement jsonElement, ILogger? logger = null)
    {
        if (jsonElement.ValueKind != JsonValueKind.Object)
        {
            throw new MessageParseException(
                $"Invalid message data type (expected Object, got {jsonElement.ValueKind})",
                jsonElement);
        }

        if (!jsonElement.TryGetProperty("type", out JsonElement messageTypeEle))
        {
            throw new MessageParseException("Message 'type' field is not found", jsonElement);
        }

        var messageType = messageTypeEle.GetString();
        if (string.IsNullOrWhiteSpace(messageType))
        {
            throw new MessageParseException("Message 'type' field is null or empty", jsonElement);
        }

        return messageType switch
        {
            "system" => ParseSystemMessage(jsonElement),
            "assistant" => ParseAssistantMessage(jsonElement),
            "user" => ParseUserMessage(jsonElement),
            "result" => ParseResultMessage(jsonElement),
            _ => throw new MessageParseException($"Unknown message type: {messageType}", jsonElement)
        };
    }


    private static UserMessage ParseUserMessage(JsonElement data)
    {
        try
        {
            if (!data.TryGetProperty("message", out var messageElement) ||
                !messageElement.TryGetProperty("content", out var contentElement))
            {
                throw new MessageParseException("Missing required field in user message: content", data);
            }

            object content;

            if (contentElement.ValueKind == JsonValueKind.Array)
            {
                var contentBlocks = new List<IContentBlock>();
                foreach (var blockElement in contentElement.EnumerateArray())
                {
                    contentBlocks.Add(ParseContentBlock(blockElement));
                }
                content = contentBlocks;
            }
            else if (contentElement.ValueKind == JsonValueKind.String)
            {
                content = contentElement.GetString()!;
            }
            else
            {
                throw new MessageParseException("Invalid content type in user message", data);
            }

            return new UserMessage
            {
                Id = GetRequiredString(data, "uuid"),
                Content = content 
            };
        }
        catch (Exception ex) when (ex is not MessageParseException)
        {
            throw new MessageParseException($"Error parsing user message: {ex.Message}", data);
        }
    }

    private static AssistantMessage ParseAssistantMessage(JsonElement data)
    {
        try
        {
            if (!data.TryGetProperty("uuid", out var uuidElement))
            {
                throw new MessageParseException("Missing 'uuid' field in assistant message", data);
            }

            if (!data.TryGetProperty("message", out var messageElement))
            {
                throw new MessageParseException("Missing 'message' field in assistant message", data);
            }

            if (!messageElement.TryGetProperty("content", out var contentElement))
            {
                throw new MessageParseException("Missing 'content' field in assistant message", data);
            }

            if (!messageElement.TryGetProperty("model", out var modelElement))
            {
                throw new MessageParseException("Missing 'model' field in assistant message", data);
            }

            if (!data.TryGetProperty("session_id", out var sessionIdElement))
            {
                throw new MessageParseException("Missing 'session_id' field in assistant message", data);
            }

            if (data.TryGetProperty("error", out var errorElement))
            {
                var errorString = errorElement.GetString()!;
                if (!string.IsNullOrWhiteSpace(errorString))
                {
                    return new AssistantMessage
                    {
                        Id = uuidElement.GetString()!,
                        Content = [new ErrorContentBlock(errorString)],
                        Model = modelElement.GetString()!,
                        SessionId = sessionIdElement.GetString()!,
                    };
                }
            }


            var contentBlocks = new List<IContentBlock>();
            foreach (var blockElement in contentElement.EnumerateArray())
            {
                contentBlocks.Add(ParseContentBlock(blockElement));
            }

            return new AssistantMessage
            {
                Id = uuidElement.GetString()!,
                Content = contentBlocks,
                Model = modelElement.GetString()!,
                SessionId = sessionIdElement.GetString()!,
            };
        }
        catch (Exception ex) when (ex is not MessageParseException)
        {
            throw new MessageParseException($"Error parsing assistant message: {ex.Message}", data);
        }
    }

    private static SystemMessage ParseSystemMessage(JsonElement data)
    {
        try
        {
            if (!data.TryGetProperty("subtype", out var subtypeElement))
            {
                throw new MessageParseException("Missing 'subtype' field in system message", data);
            }

            if (!data.TryGetProperty("session_id", out var sessionIdElement))
            {
                throw new MessageParseException("Missing 'session_id' field in system message", data);
            }
            if (!data.TryGetProperty("uuid", out var uuidElement))
            {
                throw new MessageParseException("Missing 'uuid' field in system message", data);
            }

            var dataDict = JsonUtil.SnakeCaseDeserialize<Dictionary<string, object>>(data.GetRawText());
            dataDict.Remove("subtype");

            return new SystemMessage
            {
                Id = uuidElement.GetString()!,
                Subtype = subtypeElement.GetString()!,
                SessionId = sessionIdElement.GetString()!,
                Data = dataDict
            };
        }
        catch (Exception ex) when (ex is not MessageParseException)
        {
            throw new MessageParseException($"Error parsing system message: {ex.Message}", data);
        }
    }

    private static ResultMessage ParseResultMessage(JsonElement data)
    {
        try
        {
            var result = new ResultMessage
            {
                Id = GetRequiredString(data, "uuid"),
                Subtype = GetRequiredString(data, "subtype"),
                DurationApiMs = GetRequiredInt32(data, "duration_api_ms"),
                DurationMs = GetRequiredInt32(data, "duration_ms"),
                IsError = GetRequiredBoolean(data, "is_error"),
                NumTurns = GetRequiredInt32(data, "num_turns"),
                SessionId = GetRequiredString(data, "session_id"),
                TotalCostUsd = GetOptionalDouble(data, "total_cost_usd"),
                Usage = GetOptional<Usage>(data, "usage"),
                Result = GetOptionalString(data, "result")
            };

            return result;
        }
        catch (Exception ex) when (ex is not MessageParseException)
        {
            throw new MessageParseException($"Error parsing result message: {ex.Message}", data);
        }
    }

    private static IContentBlock ParseContentBlock(JsonElement blockElement)
    {
        if (!blockElement.TryGetProperty("type", out var typeElement))
        {
            throw new MessageParseException("Content block missing 'type' field", blockElement);
        }

        var blockType = typeElement.GetString();

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
                IsError = GetOptionalBoolean(blockElement, "is_error")
            },
            _ => throw new MessageParseException($"Unknown content block type: {blockType}", blockElement)
        };
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

    private static T? GetOptional<T>(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.Deserialize<T>(JsonUtil.SNAKECASELOWER_OPTIONS);
        }
        return default(T);
    }
}