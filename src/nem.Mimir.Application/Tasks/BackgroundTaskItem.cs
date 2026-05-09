namespace nem.Mimir.Application.Tasks;

using AgentTask = global::nem.Contracts.Agents.AgentTask;

public sealed record BackgroundTaskItem(
    AgentTask Task,
    CancellationTokenSource CancellationTokenSource,
    IProgress<TaskProgress>? Progress);
