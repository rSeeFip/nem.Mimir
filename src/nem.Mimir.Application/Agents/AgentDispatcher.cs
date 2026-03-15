using System.Collections.Concurrent;
using nem.Contracts.Agents;
using ContractSpecialistAgent = nem.Contracts.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Agents;

public sealed class AgentDispatcher
{
    private readonly ConcurrentDictionary<string, ContractSpecialistAgent> _agents;

    public AgentDispatcher(IEnumerable<ContractSpecialistAgent> agents)
    {
        _agents = new ConcurrentDictionary<string, ContractSpecialistAgent>(StringComparer.OrdinalIgnoreCase);

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
        var ranked = new List<(ContractSpecialistAgent Agent, int Score)>();

        foreach (var agent in _agents.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await agent.CanHandleAsync(task, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var score = ScoreAgent(agent, task);
            ranked.Add((agent, score));
        }

        if (ranked.Count == 0)
        {
            var fallback = ResolveFallbackAgent();
            return fallback is null
                ? Array.Empty<ContractSpecialistAgent>()
                : new[] { fallback };
        }

        return ranked
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Agent.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Agent)
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

    private static int ScoreAgent(ContractSpecialistAgent agent, AgentTask task)
    {
        var score = 0;
        var prompt = task.Prompt;

        foreach (var capability in agent.Capabilities)
        {
            score += 10;
            score += capability switch
            {
                AgentCapability.CodeExploration when ContainsAny(prompt, "code", "repository", "refactor", "class", "method") => 20,
                AgentCapability.KnowledgeRetrieval when ContainsAny(prompt, "find", "lookup", "docs", "knowledge", "retrieve") => 20,
                AgentCapability.DeepAnalysis when ContainsAny(prompt, "analyze", "reason", "complex", "tradeoff", "architecture") => 20,
                AgentCapability.CodeExecution when ContainsAny(prompt, "run", "execute", "build", "test", "compile") => 20,
                AgentCapability.WebResearch when ContainsAny(prompt, "web", "internet", "online", "search", "news") => 20,
                AgentCapability.DataProcessing when ContainsAny(prompt, "transform", "data", "parse", "aggregate") => 20,
                AgentCapability.ToolInvocation when ContainsAny(prompt, "tool", "api", "service", "integration") => 20,
                _ => 0,
            };
        }

        score += task.Type switch
        {
            AgentTaskType.Explore when agent.Capabilities.Contains(AgentCapability.CodeExploration) => 15,
            AgentTaskType.Research when agent.Capabilities.Contains(AgentCapability.KnowledgeRetrieval) => 15,
            AgentTaskType.Analyze when agent.Capabilities.Contains(AgentCapability.DeepAnalysis) => 15,
            AgentTaskType.Execute when agent.Capabilities.Contains(AgentCapability.CodeExecution) => 15,
            _ => 0,
        };

        if (agent.Name.Contains("general", StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
        }

        return score;
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

    private static bool ContainsAny(string value, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (value.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
