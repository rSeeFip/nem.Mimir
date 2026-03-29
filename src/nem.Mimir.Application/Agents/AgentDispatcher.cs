using System.Collections.Concurrent;
using nem.Contracts.Agents;
using nem.Mimir.Application.Agents.Selection;
using AppSpecialistAgent = nem.Mimir.Application.Agents.ISpecialistAgent;
using ContractSpecialistAgent = nem.Contracts.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Agents;

public sealed class AgentDispatcher
{
    private readonly ConcurrentDictionary<string, ContractSpecialistAgent> _agents;
    private readonly IReadOnlyList<ISelectionStep> _selectionSteps;

    public AgentDispatcher(IEnumerable<ContractSpecialistAgent> agents)
        : this(agents, [])
    {
    }

    public AgentDispatcher(IEnumerable<ContractSpecialistAgent> agents, IEnumerable<ISelectionStep> selectionSteps)
    {
        _agents = new ConcurrentDictionary<string, ContractSpecialistAgent>(StringComparer.OrdinalIgnoreCase);
        _selectionSteps = selectionSteps.ToList();

        foreach (var agent in agents)
        {
            _agents[agent.Name] = agent;
        }
    }

    public Task RegisterAsync(ContractSpecialistAgent agent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _agents[agent.Name] = agent;
        return Task.CompletedTask;
    }

    public Task<ContractSpecialistAgent?> GetAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _agents.TryGetValue(agentId, out var agent);
        return Task.FromResult(agent);
    }

    public Task<IReadOnlyList<ContractSpecialistAgent>> ListAgentsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ContractSpecialistAgent>>(_agents.Values.ToList());
    }

    public async Task<IReadOnlyList<ContractSpecialistAgent>> GetCandidatesAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        var candidates = new List<ScoredAgent>();

        foreach (var agent in _agents.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await agent.CanHandleAsync(task, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            candidates.Add(ScoredAgent.Create(AsApplicationAgent(agent)));
        }

        if (candidates.Count == 0)
        {
            var fallback = ResolveFallbackAgent();
            return fallback is null
                ? Array.Empty<ContractSpecialistAgent>()
                : new[] { fallback };
        }

        var context = new SelectionContext(task, candidates);
        foreach (var step in _selectionSteps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context = await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }

        return context.Candidates
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Agent.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static x => (ContractSpecialistAgent)x.Agent)
            .ToList();
    }

    public async Task<ContractSpecialistAgent> DispatchAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        var candidates = await GetCandidatesAsync(task, cancellationToken).ConfigureAwait(false);
        if (candidates.Count > 0)
        {
            return candidates[0];
        }

        throw new InvalidOperationException("No specialist agents are registered.");
    }

    private static AppSpecialistAgent AsApplicationAgent(ContractSpecialistAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return agent as AppSpecialistAgent ?? new ContractSpecialistAgentAdapter(agent);
    }

    private ContractSpecialistAgent? ResolveFallbackAgent()
    {
        foreach (var entry in _agents)
        {
            if (entry.Key.Contains("general", StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        return _agents.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }

    private sealed class ContractSpecialistAgentAdapter : AppSpecialistAgent
    {
        private readonly ContractSpecialistAgent _inner;

        public ContractSpecialistAgentAdapter(ContractSpecialistAgent inner)
        {
            _inner = inner;
        }

        public string Name => _inner.Name;

        public string Description => _inner.Description;

        public IReadOnlyList<AgentCapability> Capabilities => _inner.Capabilities;

        public Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
            => _inner.ExecuteAsync(task, cancellationToken);

        public Task<bool> CanHandleAsync(AgentTask task, CancellationToken cancellationToken = default)
            => _inner.CanHandleAsync(task, cancellationToken);
    }
}
