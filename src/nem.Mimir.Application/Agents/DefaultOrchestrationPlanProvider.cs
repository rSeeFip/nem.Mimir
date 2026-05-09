using nem.Contracts.Agents;
using nem.Mimir.Application.Agents.Selection;
using nem.Mimir.Application.Common.Interfaces;

namespace nem.Mimir.Application.Agents;

public sealed class DefaultOrchestrationPlanProvider : IOrchestrationPlanProvider
{
    private static readonly IOrchestrationPlan DefaultPlan = new ProcessOrchestrationPlan(
        defaultMaxTurns: 10,
        selectionProcess: SelectionProcessDefinition.Default,
        tierConfiguration: TierConfiguration.Default);

    public IOrchestrationPlan ResolvePlan(AgentExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return DefaultPlan;
    }
}
