using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using nem.Contracts.Agents;
using nem.Contracts.Identity;
using nem.Mimir.Application.Agents;
using nem.Mimir.Application.Agents.Services;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Mcp;
using nem.Mimir.Domain.Plugins;
using nem.Mimir.Domain.Tools;
using nem.Mimir.Infrastructure.Workflow;

namespace nem.Mimir.Application.Tests.Agents;

public sealed class WorkflowBackedAgentOrchestrationBridgeTests
{
    [Fact]
    public async Task WorkflowBridge_ShouldRouteThroughMimirFacades_AndRecordTrajectory()
    {
        var llmService = Substitute.For<ILlmService>();
        llmService.SendMessageAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("draft-response", "standard", 5, 7, 12, "stop"));

        var toolCatalog = Substitute.For<IMcpToolCatalogService>();
        toolCatalog.InvokeToolAsync("catalog.lookup", Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(new ToolInvocationResult(true, "tool-result", null));

        var pluginService = Substitute.For<IPluginService>();
        pluginService.ExecutePluginAsync("plugin.echo", Arg.Any<PluginContext>(), Arg.Any<CancellationToken>())
            .Returns(PluginResult.Success(new Dictionary<string, object> { ["payload"] = "plugin-result" }));

        var recorder = Substitute.For<ITrajectoryRecorder>();
        var coordinator = new AgentCoordinator(llmService);
        var logger = Substitute.For<ILogger<WorkflowOrchestrationBridge>>();
        var bridge = new WorkflowOrchestrationBridge(
            llmService,
            toolCatalog,
            pluginService,
            coordinator,
            recorder,
            new PrivateWorkflowOrchestrationStrategy(),
            logger);

        var plan = new DefaultOrchestrationPlanProvider().ResolvePlan(new AgentExecutionContext(new AgentTask("seed", AgentTaskType.Custom, "seed")));
        var workflowExecution = new WorkflowBackedOrchestrationDefinition("""
            {
              "name":"mimir-bridge",
              "author":"tests",
              "steps":[
                {
                  "key":"draft",
                  "label":"Draft",
                  "nextStepKeys":["tool"],
                  "inputs":{},
                  "outputs":{},
                  "errorHandling":{},
                  "definition":{
                    "type":"llm",
                    "model":"standard",
                    "promptTemplate":"Prompt: {{task.prompt}}"
                  }
                },
                {
                  "key":"tool",
                  "label":"Tool",
                  "nextStepKeys":["plugin"],
                  "inputs":{
                    "query":{
                      "kind":"expression",
                      "expression":"steps.draft.output"
                    }
                  },
                  "outputs":{},
                  "errorHandling":{},
                  "definition":{
                    "type":"script",
                    "language":"mimir-tool",
                    "script":"catalog.lookup"
                  }
                },
                {
                  "key":"plugin",
                  "label":"Plugin",
                  "nextStepKeys":["delegate"],
                  "inputs":{
                    "payload":{
                      "kind":"expression",
                      "expression":"steps.tool.output"
                    }
                  },
                  "outputs":{},
                  "errorHandling":{},
                  "definition":{
                    "type":"script",
                    "language":"mimir-plugin",
                    "script":"plugin.echo"
                  }
                },
                {
                  "key":"delegate",
                  "label":"Delegate",
                  "nextStepKeys":[],
                  "inputs":{
                    "prompt":{
                      "kind":"expression",
                      "expression":"steps.plugin.payload"
                    }
                  },
                  "outputs":{},
                  "errorHandling":{},
                  "definition":{
                    "type":"script",
                    "language":"mimir-agent",
                    "script":"specialist"
                  }
                }
              ],
              "edges":[]
            }
            """);
        var task = new AgentTask(
            "task-bridge",
            AgentTaskType.Analyze,
            "inspect deployment",
            new Dictionary<string, string>());

        var context = new AgentExecutionContext(task);
        var candidate = new TestAgent(
            "specialist",
            [AgentCapability.DeepAnalysis],
            static _ => true,
            static taskToExecute => new AgentResult(taskToExecute.Id, "specialist", "Completed", $"delegated:{taskToExecute.Prompt}"));

        var result = await bridge.ExecuteAsync(context, plan, workflowExecution, [candidate], TrajectoryId.New(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe("Completed", result.Output);
        result.AgentId.ShouldBe("specialist");
        result.Output.ShouldBe("delegated:plugin-result");

        await llmService.Received(1).SendMessageAsync(
            "standard",
            Arg.Is<IReadOnlyList<LlmMessage>>(messages => messages.Count == 1 && messages[0].Content == "Prompt: inspect deployment"),
            Arg.Any<CancellationToken>());
        await toolCatalog.Received(1).InvokeToolAsync(
            "catalog.lookup",
            Arg.Is<Dictionary<string, object>>(arguments => arguments.ContainsKey("query") && arguments["query"].ToString() == "draft-response"),
            Arg.Any<CancellationToken>());
        await pluginService.Received(1).ExecutePluginAsync(
            "plugin.echo",
            Arg.Is<PluginContext>(pluginContext => pluginContext.Parameters.ContainsKey("payload") && pluginContext.Parameters["payload"].ToString() == "tool-result"),
            Arg.Any<CancellationToken>());
        await recorder.Received(4).RecordStepAsync(
            Arg.Any<TrajectoryId>(),
            Arg.Any<nem.Mimir.Domain.ValueObjects.TrajectoryStep>(),
            Arg.Any<CancellationToken>());
    }

    private sealed class TestAgent : nem.Contracts.Agents.ISpecialistAgent
    {
        private readonly Func<AgentTask, bool> _canHandle;
        private readonly Func<AgentTask, AgentResult> _execute;

        public TestAgent(
            string name,
            IReadOnlyList<AgentCapability> capabilities,
            Func<AgentTask, bool> canHandle,
            Func<AgentTask, AgentResult> execute)
        {
            Name = name;
            Description = name;
            Capabilities = capabilities;
            _canHandle = canHandle;
            _execute = execute;
        }

        public string Name { get; }

        public string Description { get; }

        public IReadOnlyList<AgentCapability> Capabilities { get; }

        public Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
            => Task.FromResult(_execute(task));

        public Task<bool> CanHandleAsync(AgentTask task, CancellationToken cancellationToken = default)
            => Task.FromResult(_canHandle(task));
    }

    private sealed class PrivateWorkflowOrchestrationStrategy : nem.Workflow.Domain.Interfaces.IOrchestrationStrategy
    {
        public IReadOnlyList<nem.Workflow.Domain.Models.WorkflowStep> ResolveNextSteps(
            nem.Workflow.Domain.Interfaces.WorkflowOrchestrationContext runContext,
            nem.Workflow.Domain.Interfaces.WorkflowStepExecutionResult? currentStepResult = null)
        {
            var currentStepKey = currentStepResult?.StepKey ?? runContext.CurrentStepKey;
            if (string.IsNullOrWhiteSpace(currentStepKey))
            {
                return runContext.Definition.Steps.Take(1).ToList();
            }

            var currentStep = runContext.Definition.Steps.First(step => step.Key == currentStepKey);
            return runContext.Definition.Steps
                .Where(step => currentStep.NextStepKeys.Contains(step.Key, StringComparer.Ordinal))
                .ToList();
        }
    }
}
