using nem.Contracts.Agents;
using nem.Contracts.Identity;
using nem.Mimir.Application.Agents;
using ContractSpecialistAgent = nem.Contracts.Agents.ISpecialistAgent;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IWorkflowOrchestrationBridge
{
    Task<AgentResult> ExecuteAsync(
        AgentExecutionContext context,
        IOrchestrationPlan plan,
        WorkflowBackedOrchestrationDefinition workflowDefinition,
        IReadOnlyList<ContractSpecialistAgent> candidates,
        TrajectoryId trajectoryId,
        CancellationToken cancellationToken = default);
}
