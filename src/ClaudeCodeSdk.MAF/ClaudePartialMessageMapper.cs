using ClaudeCodeSdk.Types;
using ClaudeCodeSdk.Utils;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;

namespace ClaudeCodeSdk.MAF;

internal sealed class ClaudePartialMessageMapper
{
    private readonly string _responseId = Guid.NewGuid().ToString("N");
    private readonly Dictionary<StreamKey, MessageState> _activeMessages = [];
    private readonly Dictionary<string, MessageState> _messagesById = new(StringComparer.Ordinal);
    private readonly HashSet<string> _streamStoppedMessageIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _assistantSnapshotMessageIds = new(StringComparer.Ordinal);
    private string? _lastStoppedMessageId;
    private string? _lastAssistantMessageId;

    public IEnumerable<AgentResponseUpdate> Map(IMessage message)
    {
        _lastStoppedMessageId = null;
        _lastAssistantMessageId = null;
        if (message is StreamEvent streamEvent)
        {
            if (MapStreamEvent(streamEvent) is { } update)
            {
                yield return update;
            }
            yield break;
        }

        if (message is AssistantMessage assistantMessage)
        {
            if (MapAssistantMessage(assistantMessage) is { } update)
            {
                yield return update;
            }
            yield break;
        }

        if (message.ToAgentRunResponseUpdate() is { } defaultUpdate)
        {
            defaultUpdate.ResponseId ??= _responseId;
            yield return defaultUpdate;
        }

        if (message is ResultMessage)
        {
            _activeMessages.Clear();
            _messagesById.Clear();
            _streamStoppedMessageIds.Clear();
            _assistantSnapshotMessageIds.Clear();
        }
    }

    internal bool TryConsumeCompletedMessageId(IMessage message, out string messageId)
    {
        messageId = message switch
        {
            AssistantMessage => _lastAssistantMessageId ?? string.Empty,
            StreamEvent => _lastStoppedMessageId ?? string.Empty,
            _ => string.Empty,
        };
        if (
            messageId.Length == 0
            || !_streamStoppedMessageIds.Contains(messageId)
            || !_assistantSnapshotMessageIds.Contains(messageId)
        )
        {
            return false;
        }

        _streamStoppedMessageIds.Remove(messageId);
        _assistantSnapshotMessageIds.Remove(messageId);
        if (_messagesById.Remove(messageId, out var state))
        {
            if (
                _activeMessages.TryGetValue(state.Key, out var activeState)
                && ReferenceEquals(activeState, state)
            )
            {
                _activeMessages.Remove(state.Key);
            }
        }
        return true;
    }

    private AgentResponseUpdate? MapStreamEvent(StreamEvent streamEvent)
    {
        if (!TryGetString(streamEvent.Event, "type", out var eventType))
        {
            return null;
        }

        return eventType switch
        {
            "message_start" => StartMessage(streamEvent),
            "content_block_start" => StartContentBlock(streamEvent),
            "content_block_delta" => AppendContentBlockDelta(streamEvent),
            "content_block_stop" => StopContentBlock(streamEvent),
            "message_delta" => MapMessageDelta(streamEvent),
            "message_stop" => StopMessage(streamEvent),
            _ => null,
        };
    }

    private AgentResponseUpdate? StartMessage(StreamEvent streamEvent)
    {
        if (
            !streamEvent.Event.TryGetProperty("message", out var message)
            || !TryGetString(message, "id", out var messageId)
            || !TryGetString(message, "model", out var model)
        )
        {
            return null;
        }

        var key = new StreamKey(streamEvent.SessionId, streamEvent.ParentToolUseId);
        var state = new MessageState(key, messageId, model);
        _activeMessages[key] = state;
        _messagesById[messageId] = state;
        return null;
    }

    private AgentResponseUpdate? StartContentBlock(StreamEvent streamEvent)
    {
        if (
            !TryGetActiveMessage(streamEvent, out var state)
            || !TryGetIndex(streamEvent.Event, out var index)
            || !streamEvent.Event.TryGetProperty("content_block", out var contentBlock)
            || !TryGetString(contentBlock, "type", out var blockType)
        )
        {
            return null;
        }

        var block = new ContentBlockState(blockType);
        state.ContentBlocks[index] = block;
        switch (blockType)
        {
            case "text" when TryGetString(contentBlock, "text", out var text) && text.Length > 0:
                state.EmittedContentIndexes.Add(index);
                return CreateUpdate(
                    state,
                    new TextContent(text) { RawRepresentation = streamEvent },
                    streamEvent
                );
            case "thinking"
                when TryGetString(contentBlock, "thinking", out var thinking)
                    && thinking.Length > 0:
                state.EmittedContentIndexes.Add(index);
                return CreateUpdate(
                    state,
                    new TextReasoningContent(thinking) { RawRepresentation = streamEvent },
                    streamEvent
                );
            case "tool_use":
                block.ToolCallId = GetOptionalString(contentBlock, "id");
                block.ToolName = GetOptionalString(contentBlock, "name");
                if (contentBlock.TryGetProperty("input", out var input))
                {
                    block.InitialInput = input.Clone();
                }
                break;
        }

        return null;
    }

    private AgentResponseUpdate? AppendContentBlockDelta(StreamEvent streamEvent)
    {
        if (
            !TryGetActiveMessage(streamEvent, out var state)
            || !TryGetIndex(streamEvent.Event, out var index)
            || !streamEvent.Event.TryGetProperty("delta", out var delta)
            || !TryGetString(delta, "type", out var deltaType)
        )
        {
            return null;
        }

        if (!state.ContentBlocks.TryGetValue(index, out var block))
        {
            block = new ContentBlockState(deltaType);
            state.ContentBlocks[index] = block;
        }

        switch (deltaType)
        {
            case "text_delta" when TryGetString(delta, "text", out var text) && text.Length > 0:
                state.EmittedContentIndexes.Add(index);
                return CreateUpdate(
                    state,
                    new TextContent(text) { RawRepresentation = streamEvent },
                    streamEvent
                );
            case "thinking_delta"
                when TryGetString(delta, "thinking", out var thinking) && thinking.Length > 0:
                state.EmittedContentIndexes.Add(index);
                return CreateUpdate(
                    state,
                    new TextReasoningContent(thinking) { RawRepresentation = streamEvent },
                    streamEvent
                );
            case "input_json_delta" when TryGetString(delta, "partial_json", out var partialJson):
                block.PartialJson.Append(partialJson);
                break;
        }

        return null;
    }

    private AgentResponseUpdate? StopContentBlock(StreamEvent streamEvent)
    {
        if (
            !TryGetActiveMessage(streamEvent, out var state)
            || !TryGetIndex(streamEvent.Event, out var index)
            || !state.ContentBlocks.TryGetValue(index, out var block)
            || !string.Equals(block.Type, "tool_use", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(block.ToolCallId)
            || string.IsNullOrWhiteSpace(block.ToolName)
            || !TryGetToolArguments(block, out var arguments)
        )
        {
            return null;
        }

        state.EmittedContentIndexes.Add(index);
        var functionCall = new FunctionCallContent(block.ToolCallId, block.ToolName, arguments)
        {
            RawRepresentation = streamEvent,
        };
        return CreateUpdate(state, functionCall, streamEvent);
    }

    private AgentResponseUpdate? MapMessageDelta(StreamEvent streamEvent)
    {
        if (
            !TryGetActiveMessage(streamEvent, out var state)
            || !streamEvent.Event.TryGetProperty("delta", out var delta)
            || !TryGetString(delta, "stop_reason", out var stopReason)
        )
        {
            return null;
        }

        var update = CreateUpdate(state, content: null, streamEvent);
        update.FinishReason = stopReason switch
        {
            "end_turn" => ChatFinishReason.Stop,
            "max_tokens" => ChatFinishReason.Length,
            "tool_use" => ChatFinishReason.ToolCalls,
            _ => new ChatFinishReason(stopReason),
        };
        return update;
    }

    private AgentResponseUpdate? StopMessage(StreamEvent streamEvent)
    {
        if (TryGetActiveMessage(streamEvent, out var state))
        {
            _streamStoppedMessageIds.Add(state.MessageId);
            _lastStoppedMessageId = state.MessageId;
        }

        return null;
    }

    private AgentResponseUpdate? MapAssistantMessage(AssistantMessage message)
    {
        var state = FindMessageState(message);
        var messageId = state?.MessageId ?? message.ApiMessageId ?? message.Id;
        _lastAssistantMessageId = messageId;
        if (messageId.Length > 0)
        {
            _assistantSnapshotMessageIds.Add(messageId);
        }

        if (state != null)
        {
            if (state.EmittedContentIndexes.Count > 0)
            {
                var remaining = message
                    .Content.Where((_, index) => !state.EmittedContentIndexes.Contains(index))
                    .ToArray();
                if (remaining.Length == 0)
                {
                    return null;
                }

                var remainingUpdate = IMessageExtension.CreateAssistantUpdate(message, remaining);
                remainingUpdate.ResponseId = _responseId;
                remainingUpdate.MessageId = state.MessageId;
                return remainingUpdate;
            }
        }

        var fallback = IMessageExtension.CreateAssistantUpdate(message, message.Content);
        fallback.ResponseId = _responseId;
        fallback.MessageId = messageId;
        return fallback;
    }

    private MessageState? FindMessageState(AssistantMessage message)
    {
        if (
            message.ApiMessageId is { Length: > 0 } apiMessageId
            && _messagesById.TryGetValue(apiMessageId, out var stateById)
        )
        {
            return stateById;
        }

        return _activeMessages.GetValueOrDefault(
            new StreamKey(message.SessionId, message.ParentToolUseId)
        );
    }

    private AgentResponseUpdate CreateUpdate(
        MessageState state,
        AIContent? content,
        StreamEvent streamEvent
    ) =>
        new(ChatRole.Assistant, content == null ? null : [content])
        {
            AuthorName = state.Model,
            ResponseId = _responseId,
            MessageId = state.MessageId,
            RawRepresentation = streamEvent,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["agentName"] = IMessageExtension.AgentName,
                ["type"] = MessageType.Assistant.Value,
            },
        };

    private bool TryGetActiveMessage(StreamEvent streamEvent, out MessageState state) =>
        _activeMessages.TryGetValue(
            new StreamKey(streamEvent.SessionId, streamEvent.ParentToolUseId),
            out state!
        );

    private static bool TryGetToolArguments(
        ContentBlockState block,
        out IDictionary<string, object?>? arguments
    )
    {
        try
        {
            var json =
                block.PartialJson.Length > 0
                    ? block.PartialJson.ToString()
                    : block.InitialInput?.GetRawText();
            arguments = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtil.Deserialize<Dictionary<string, object?>>(json);
            return true;
        }
        catch (JsonException)
        {
            arguments = null;
            return false;
        }
    }

    private static bool TryGetIndex(JsonElement element, out int index)
    {
        if (element.TryGetProperty("index", out var value) && value.TryGetInt32(out index))
        {
            return true;
        }

        index = default;
        return false;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = GetOptionalString(element, propertyName) ?? string.Empty;
        return value.Length > 0;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private readonly record struct StreamKey(string SessionId, string? ParentToolUseId);

    private sealed class MessageState
    {
        public MessageState(StreamKey key, string messageId, string model)
        {
            Key = key;
            MessageId = messageId;
            Model = model;
        }

        public StreamKey Key { get; }
        public string MessageId { get; }
        public string Model { get; }
        public Dictionary<int, ContentBlockState> ContentBlocks { get; } = [];
        public HashSet<int> EmittedContentIndexes { get; } = [];
    }

    private sealed class ContentBlockState
    {
        public ContentBlockState(string type)
        {
            Type = type;
        }

        public string Type { get; }
        public string? ToolCallId { get; set; }
        public string? ToolName { get; set; }
        public JsonElement? InitialInput { get; set; }
        public StringBuilder PartialJson { get; } = new();
    }
}
