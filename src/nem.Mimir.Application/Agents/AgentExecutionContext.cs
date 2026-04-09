using System.Collections.Concurrent;
using nem.Mimir.Domain.Entities;
using nem.Contracts.Agents;

namespace nem.Mimir.Application.Agents;

public sealed class AgentExecutionContext
{
    private sealed class TurnState
    {
        public int Value;
    }

    private readonly TurnState _turnState;

    public AgentExecutionContext(
        AgentTask task,
        int maxTurns = 10,
        List<Message>? conversationHistory = null,
        ConcurrentDictionary<string, AgentResult>? toolResults = null)
        : this(task, maxTurns, conversationHistory, toolResults, new TurnState())
    {
    }

    private AgentExecutionContext(
        AgentTask task,
        int maxTurns,
        List<Message>? conversationHistory,
        ConcurrentDictionary<string, AgentResult>? toolResults,
        TurnState turnState)
    {
        if (maxTurns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTurns), "Max turns must be greater than zero.");
        }

        Task = task;
        MaxTurns = maxTurns;
        ConversationHistory = conversationHistory ?? new List<Message>();
        ToolResults = toolResults ?? new ConcurrentDictionary<string, AgentResult>(StringComparer.OrdinalIgnoreCase);
        _turnState = turnState;
    }

    public AgentTask Task { get; private set; }

    public List<Message> ConversationHistory { get; }

    public ConcurrentDictionary<string, AgentResult> ToolResults { get; }

    public int TurnCount => Volatile.Read(ref _turnState.Value);

    public int MaxTurns { get; private set; }

    public int IncrementTurn()
    {
        var next = Interlocked.Increment(ref _turnState.Value);
        if (next > MaxTurns)
        {
            throw new InvalidOperationException($"Turn limit exceeded. Max turns: {MaxTurns}.");
        }

        return next;
    }

    public void SetTask(AgentTask task)
    {
        Task = task;
    }

    public void SetMaxTurns(int maxTurns)
    {
        if (maxTurns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTurns), "Max turns must be greater than zero.");
        }

        MaxTurns = maxTurns;
    }

    public AgentExecutionContext CreateChild(AgentTask task)
    {
        return new AgentExecutionContext(task, MaxTurns, ConversationHistory, ToolResults, _turnState);
    }
}
