using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ClaudeCodeSdk.MAF;

/// <summary>
/// Buffers streaming updates until a complete Claude assistant message can be persisted safely.
/// </summary>
internal sealed class ClaudeStreamingHistoryAccumulator
{
    private readonly Dictionary<string, List<SequencedUpdate>> _assistantUpdates = new(
        StringComparer.Ordinal
    );
    private readonly List<SequencedUpdate> _contextUpdates = [];
    private readonly List<SequencedUpdate> _unidentifiedAssistantUpdates = [];
    private long _nextSequence;

    public void Add(IEnumerable<AgentResponseUpdate> updates)
    {
        foreach (var update in updates)
        {
            var sequencedUpdate = new SequencedUpdate(_nextSequence++, update);
            if (update.Role != ChatRole.Assistant)
            {
                _contextUpdates.Add(sequencedUpdate);
            }
            else if (update.MessageId is { Length: > 0 } messageId)
            {
                if (!_assistantUpdates.TryGetValue(messageId, out var messageUpdates))
                {
                    messageUpdates = [];
                    _assistantUpdates[messageId] = messageUpdates;
                }

                messageUpdates.Add(sequencedUpdate);
            }
            else
            {
                _unidentifiedAssistantUpdates.Add(sequencedUpdate);
            }
        }
    }

    public IReadOnlyList<AgentResponseUpdate> CompleteAssistantMessage(string messageId)
    {
        var completedUpdates = new List<SequencedUpdate>(_contextUpdates);
        _contextUpdates.Clear();
        if (_assistantUpdates.Remove(messageId, out var assistantUpdates))
        {
            completedUpdates.AddRange(assistantUpdates);
        }

        return Order(completedUpdates);
    }

    public IReadOnlyList<AgentResponseUpdate> CompleteRun()
    {
        var completedUpdates = new List<SequencedUpdate>(_contextUpdates);
        _contextUpdates.Clear();
        completedUpdates.AddRange(_unidentifiedAssistantUpdates);
        _unidentifiedAssistantUpdates.Clear();
        foreach (var assistantUpdates in _assistantUpdates.Values)
        {
            completedUpdates.AddRange(assistantUpdates);
        }
        _assistantUpdates.Clear();

        return Order(completedUpdates);
    }

    private static IReadOnlyList<AgentResponseUpdate> Order(IEnumerable<SequencedUpdate> updates) =>
        updates
            .OrderBy(static update => update.Sequence)
            .Select(static update => update.Update)
            .ToList();

    private readonly record struct SequencedUpdate(long Sequence, AgentResponseUpdate Update);
}
