using System.Collections.Concurrent;
using nem.Contracts.Agents;
using nem.Mimir.Application.Agents.Selection;
using nem.Mimir.Application.Common.Interfaces;
using AppSpecialistAgent = nem.Mimir.Application.Agents.ISpecialistAgent;
using ContractSpecialistAgent = nem.Contracts.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Agents;

public sealed class AgentDispatcher
{
    private readonly ConcurrentDictionary<string, ContractSpecialistAgent> _agents;
    private readonly IReadOnlyDictionary<string, ISelectionStep> _selectionSteps;
    private readonly IOrchestrationPlanProvider _planProvider;

    public AgentDispatcher(IEnumerable<ContractSpecialistAgent> agents)
        : this(agents, [], new DefaultOrchestrationPlanProvider())
    {
    }

    public AgentDispatcher(IEnumerable<ContractSpecialistAgent> agents, IEnumerable<ISelectionStep> selectionSteps)
        : this(agents, selectionSteps, new DefaultOrchestrationPlanProvider())
    {
    }

    public AgentDispatcher(
        IEnumerable<ContractSpecialistAgent> agents,
        IEnumerable<ISelectionStep> selectionSteps,
        IOrchestrationPlanProvider planProvider)
    {
        _agents = new ConcurrentDictionary<string, ContractSpecialistAgent>(StringComparer.OrdinalIgnoreCase);
        _selectionSteps = selectionSteps.ToDictionary(step => step.Name, StringComparer.OrdinalIgnoreCase);
        _planProvider = planProvider;

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
            var fallback = ResolveFallbackAgent(task);
            return fallback is null
                ? Array.Empty<ContractSpecialistAgent>()
                : new[] { fallback };
        }

        var plan = _planProvider.ResolvePlan(new AgentExecutionContext(task));
        var context = new SelectionContext(task, candidates, plan.SelectionProcess);

        if (_selectionSteps.Count == 0)
        {
            return context.Candidates
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Agent.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static x => (ContractSpecialistAgent)x.Agent)
                .ToList();
        }

        foreach (var stepDefinition in plan.SelectionProcess.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_selectionSteps.TryGetValue(stepDefinition.Name, out var step))
            {
                throw new InvalidOperationException($"Selection step '{stepDefinition.Name}' is not registered.");
            }

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

    private ContractSpecialistAgent? ResolveFallbackAgent(AgentTask task)
    {
        var plan = _planProvider.ResolvePlan(new AgentExecutionContext(task));

        foreach (var pattern in plan.SelectionProcess.Fallback.PreferredAgentNamePatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            foreach (var entry in _agents)
            {
                if (entry.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }
        }

        if (!plan.SelectionProcess.Fallback.UseAlphabeticalFallback)
        {
            return null;
        }

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
