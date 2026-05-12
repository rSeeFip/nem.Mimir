using nem.Mimir.Application.Agents;

namespace nem.Mimir.Application.Common.Interfaces;

public interface IOrchestrationPlanProvider
{
    IOrchestrationPlan ResolvePlan(AgentExecutionContext context);
}
