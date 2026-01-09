using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ClaudeCodeSdk;

/// <summary>
/// Message parser for Claude Code SDK responses.
/// </summary>
internal static class MessageParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Parse message from CLI output into typed Message objects.
    /// </summary>
    /// <param name="data">Raw message dictionary from CLI output</param>
    /// <param name="logger">Optional logger for debugging</param>
    /// <returns>Parsed Message object</returns>
    /// <exception cref="MessageParseException">If parsing fails or message type is unrecognized</exception>
    public static IMessage ParseMessage(Dictionary<string, object> dataDict, ILogger? logger = null)
    {
        var data = JsonSerializer.SerializeToElement(dataDict, JsonOptions);
        if (data.ValueKind != JsonValueKind.Object)
        {
            throw new MessageParseException(
                $"Invalid message data type (expected Object, got {data.ValueKind})",
                data);
        }

        if (!data.TryGetProperty("type", out JsonElement messageTypeEle))
        {
            throw new MessageParseException("Message 'type' field is not found", data);
        }

        var messageType = messageTypeEle.GetString();
        if (string.IsNullOrEmpty(messageType))
        {
            throw new MessageParseException("Message 'type' field is null or empty", dataDict);
        }


        return messageType switch
        {
            "user" => ParseUserMessage(data),
            "assistant" => ParseAssistantMessage(data),
            "system" => ParseSystemMessage(data),
            "result" => ParseResultMessage(data),
            _ => throw new MessageParseException($"Unknown message type: {messageType}", data)
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

            return new UserMessage { Content = content };
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

            var contentBlocks = new List<IContentBlock>();
            foreach (var blockElement in contentElement.EnumerateArray())
            {
                contentBlocks.Add(ParseContentBlock(blockElement));
            }

            return new AssistantMessage
            {
                Content = contentBlocks,
                Model = modelElement.GetString()!
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

            var dataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(data.GetRawText(), JsonOptions)!;

            return new SystemMessage
            {
                Subtype = subtypeElement.GetString()!,
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
                Subtype = GetRequiredString(data, "subtype"),
                DurationMs = GetRequiredInt32(data, "duration_ms"),
                DurationApiMs = GetRequiredInt32(data, "duration_api_ms"),
                IsError = GetRequiredBoolean(data, "is_error"),
                NumTurns = GetRequiredInt32(data, "num_turns"),
                SessionId = GetRequiredString(data, "session_id"),
                TotalCostUsd = GetOptionalDouble(data, "total_cost_usd"),
                Usage = GetOptionalDictionary(data, "usage"),
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
            return JsonSerializer.Deserialize<object>(prop.GetRawText(), JsonOptions);
        }
        return null;
    }

    private static Dictionary<string, object> GetRequiredDictionary(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(prop.GetRawText(), JsonOptions)!;
        }
        throw new MessageParseException($"Missing required property: {propertyName}", element);
    }

    private static Dictionary<string, object>? GetOptionalDictionary(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(prop.GetRawText(), JsonOptions);
        }
        return null;
    }
}