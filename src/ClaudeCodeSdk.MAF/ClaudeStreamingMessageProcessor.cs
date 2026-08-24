using ClaudeCodeSdk.Types;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ClaudeCodeSdk.MAF;

/// <summary>
/// Maps Claude messages to MAF updates and identifies history batches that are safe to persist.
/// </summary>
internal sealed class ClaudeStreamingMessageProcessor
{
    private readonly ClaudePartialMessageMapper? _mapper;
    private readonly ClaudeStreamingHistoryAccumulator? _historyAccumulator;
    private IReadOnlyList<ChatMessage> _unpersistedRequestMessages;
    private bool _runFailed;

    public ClaudeStreamingMessageProcessor(
        IReadOnlyList<ChatMessage> requestMessages,
        bool enableHistoryPersistence,
        bool enableMessageMapping
    )
    {
        _unpersistedRequestMessages = requestMessages;
        _mapper =
            enableHistoryPersistence || enableMessageMapping
                ? new ClaudePartialMessageMapper()
                : null;
        _historyAccumulator = enableHistoryPersistence
            ? new ClaudeStreamingHistoryAccumulator()
            : null;
    }

    public MappedClaudeMessage Process(IMessage message)
    {
        if (_mapper == null)
        {
            return new MappedClaudeMessage([], CompletedHistoryBatch: null);
        }

        var updates = _mapper.Map(message).ToList();
        _historyAccumulator?.Add(updates);
        if (message is ResultMessage resultMessage)
        {
            _runFailed |= resultMessage.IsError;
        }

        ClaudeHistoryBatch? completedBatch = null;
        if (
            _historyAccumulator != null
            && _mapper.TryConsumeCompletedMessageId(message, out var completedMessageId)
        )
        {
            var completedUpdates = _historyAccumulator.CompleteAssistantMessage(completedMessageId);
            completedBatch = CreateBatch(completedUpdates);
        }

        return new MappedClaudeMessage(updates, completedBatch);
    }

    public ClaudeHistoryBatch? CompleteRun()
    {
        if (_historyAccumulator == null || _runFailed)
        {
            return null;
        }

        return CreateBatch(_historyAccumulator.CompleteRun());
    }

    private ClaudeHistoryBatch? CreateBatch(IReadOnlyList<AgentResponseUpdate> updates)
    {
        if (updates.Count == 0 && _unpersistedRequestMessages.Count == 0)
        {
            return null;
        }

        var batch = new ClaudeHistoryBatch(_unpersistedRequestMessages, updates);
        _unpersistedRequestMessages = [];
        return batch;
    }
}

internal readonly record struct MappedClaudeMessage(
    IReadOnlyList<AgentResponseUpdate> Updates,
    ClaudeHistoryBatch? CompletedHistoryBatch
);

internal readonly record struct ClaudeHistoryBatch(
    IReadOnlyList<ChatMessage> RequestMessages,
    IReadOnlyList<AgentResponseUpdate> ResponseUpdates
);
