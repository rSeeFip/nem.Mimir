using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using nem.Contracts.Agents;
using nem.Contracts.Identity;
using nem.Mimir.Application.Agents;
using nem.Mimir.Application.Agents.Services;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Application.Mcp;
using nem.Mimir.Domain.Plugins;
using nem.Workflow.Domain.Interfaces;
using nem.Workflow.Domain.Models;
using nem.Workflow.Domain.Models.Steps;
using nem.Workflow.Domain.Models.Supporting;
using ContractSpecialistAgent = nem.Contracts.Agents.ISpecialistAgent;

namespace nem.Mimir.Infrastructure.Workflow;

internal sealed partial class WorkflowOrchestrationBridge : IWorkflowOrchestrationBridge
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ILlmService _llmService;
    private readonly IMcpToolCatalogService _toolCatalogService;
    private readonly IPluginService _pluginService;
    private readonly AgentCoordinator _agentCoordinator;
    private readonly ITrajectoryRecorder _trajectoryRecorder;
    private readonly IOrchestrationStrategy _orchestrationStrategy;
    private readonly ILogger<WorkflowOrchestrationBridge> _logger;

    public WorkflowOrchestrationBridge(
        ILlmService llmService,
        IMcpToolCatalogService toolCatalogService,
        IPluginService pluginService,
        AgentCoordinator agentCoordinator,
        ITrajectoryRecorder trajectoryRecorder,
        ILogger<WorkflowOrchestrationBridge> logger)
        : this(
            llmService,
            toolCatalogService,
            pluginService,
            agentCoordinator,
            trajectoryRecorder,
            new LocalWorkflowOrchestrationStrategy(),
            logger)
    {
    }

    internal WorkflowOrchestrationBridge(
        ILlmService llmService,
        IMcpToolCatalogService toolCatalogService,
        IPluginService pluginService,
        AgentCoordinator agentCoordinator,
        ITrajectoryRecorder trajectoryRecorder,
        IOrchestrationStrategy orchestrationStrategy,
        ILogger<WorkflowOrchestrationBridge> logger)
    {
        _llmService = llmService;
        _toolCatalogService = toolCatalogService;
        _pluginService = pluginService;
        _agentCoordinator = agentCoordinator;
        _trajectoryRecorder = trajectoryRecorder;
        _orchestrationStrategy = orchestrationStrategy;
        _logger = logger;
    }

    public async Task<AgentResult> ExecuteAsync(
        AgentExecutionContext context,
        IOrchestrationPlan plan,
        WorkflowBackedOrchestrationDefinition workflowDefinition,
        IReadOnlyList<ContractSpecialistAgent> candidates,
        TrajectoryId trajectoryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(candidates);

        NemFlowDefinition definition;

        try
        {
            definition = JsonSerializer.Deserialize<NemFlowDefinition>(workflowDefinition.DefinitionJson, SerializerOptions)
                ?? throw new InvalidOperationException("Workflow definition payload was empty.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse workflow-backed orchestration definition for task {TaskId}.", context.Task.Id);
            return new AgentResult(context.Task.Id, "workflow-bridge", "Failed", $"Workflow definition could not be parsed: {ex.Message}");
        }

        var stepResults = new Dictionary<string, StepExecutionState>(StringComparer.Ordinal);
        var pendingSteps = new Queue<WorkflowStep>(_orchestrationStrategy.ResolveNextSteps(
            new WorkflowOrchestrationContext(new nem.Workflow.Domain.Entities.WorkflowRun(WorkflowRunId.New(), definition.Id), definition, null)));
        WorkflowStep? lastStep = null;
        StepExecutionState? lastState = null;

        while (pendingSteps.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = pendingSteps.Dequeue();
            if (stepResults.ContainsKey(step.Key))
            {
                continue;
            }

            var startedAt = DateTime.UtcNow;
            var state = await ExecuteStepAsync(step, context, plan, candidates, stepResults, cancellationToken).ConfigureAwait(false);
            stepResults[step.Key] = state;
            lastStep = step;
            lastState = state;

            await _trajectoryRecorder.RecordStepAsync(
                    trajectoryId,
                    nem.Mimir.Domain.ValueObjects.TrajectoryStep.Create(
                        $"Workflow:{step.Key}",
                        DescribeStep(step),
                        state.OutputSummary,
                        DateTime.UtcNow - startedAt,
                        state.IsSuccess,
                        startedAt),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!state.IsSuccess && !step.ErrorHandling.ContinueOnError)
            {
                return state.ToAgentResult(context.Task.Id, step.Key);
            }

            foreach (var nextStep in _orchestrationStrategy.ResolveNextSteps(
                         new WorkflowOrchestrationContext(new nem.Workflow.Domain.Entities.WorkflowRun(WorkflowRunId.New(), definition.Id), definition, step.Key),
                         new WorkflowStepExecutionResult(step.Key, state.IsSuccess)))
            {
                if (!stepResults.ContainsKey(nextStep.Key))
                {
                    pendingSteps.Enqueue(nextStep);
                }
            }
        }

        return lastState?.ToAgentResult(context.Task.Id, lastStep?.Key ?? "workflow")
               ?? new AgentResult(context.Task.Id, "workflow-bridge", "Failed", "Workflow definition did not contain executable steps.");
    }

    private async Task<StepExecutionState> ExecuteStepAsync(
        WorkflowStep step,
        AgentExecutionContext context,
        IOrchestrationPlan plan,
        IReadOnlyList<ContractSpecialistAgent> candidates,
        IReadOnlyDictionary<string, StepExecutionState> stepResults,
        CancellationToken cancellationToken)
    {
        return step.Definition switch
        {
            LlmStepDefinition llmStep => await ExecuteLlmStepAsync(step, llmStep, context, stepResults, cancellationToken).ConfigureAwait(false),
            ScriptStepDefinition scriptStep => await ExecuteScriptStepAsync(step, scriptStep, context, plan, candidates, stepResults, cancellationToken).ConfigureAwait(false),
            _ => new StepExecutionState(false, $"Workflow-backed orchestration does not support step type '{step.Definition.GetType().Name}'.")
        };
    }

    private async Task<StepExecutionState> ExecuteLlmStepAsync(
        WorkflowStep step,
        LlmStepDefinition definition,
        AgentExecutionContext context,
        IReadOnlyDictionary<string, StepExecutionState> stepResults,
        CancellationToken cancellationToken)
    {
        var prompt = ResolveTemplate(definition.PromptTemplate, context, stepResults);
        var model = string.IsNullOrWhiteSpace(definition.Model) ? "standard" : definition.Model;
        var messages = new List<LlmMessage>();

        if (!string.IsNullOrWhiteSpace(definition.SystemPrompt))
        {
            messages.Add(new LlmMessage("system", ResolveTemplate(definition.SystemPrompt, context, stepResults)));
        }

        messages.Add(new LlmMessage("user", prompt));
        var response = await _llmService.SendMessageAsync(model, messages, cancellationToken).ConfigureAwait(false);

        return new StepExecutionState(
            true,
            response.Content,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["response"] = response.Content,
                ["model"] = response.Model,
                ["promptTokens"] = response.PromptTokens,
                ["completionTokens"] = response.CompletionTokens,
                ["totalTokens"] = response.TotalTokens,
                ["finishReason"] = response.FinishReason
            });
    }

    private async Task<StepExecutionState> ExecuteScriptStepAsync(
        WorkflowStep step,
        ScriptStepDefinition definition,
        AgentExecutionContext context,
        IOrchestrationPlan plan,
        IReadOnlyList<ContractSpecialistAgent> candidates,
        IReadOnlyDictionary<string, StepExecutionState> stepResults,
        CancellationToken cancellationToken)
    {
        var operation = definition.Language.Trim().ToLowerInvariant();

        return operation switch
        {
            "mimir-tool" => await ExecuteToolStepAsync(step, definition, context, stepResults, cancellationToken).ConfigureAwait(false),
            "mimir-plugin" => await ExecutePluginStepAsync(step, definition, context, stepResults, cancellationToken).ConfigureAwait(false),
            "mimir-agent" => await ExecuteAgentStepAsync(step, definition, context, plan, candidates, stepResults, cancellationToken).ConfigureAwait(false),
            _ => new StepExecutionState(false, $"Workflow-backed orchestration does not support script language '{definition.Language}'.")
        };
    }

    private async Task<StepExecutionState> ExecuteToolStepAsync(
        WorkflowStep step,
        ScriptStepDefinition definition,
        AgentExecutionContext context,
        IReadOnlyDictionary<string, StepExecutionState> stepResults,
        CancellationToken cancellationToken)
    {
        var toolName = ResolveTemplate(definition.Script, context, stepResults);
        var arguments = ResolveArguments(step, context, stepResults, ["toolName"]);
        var result = await _toolCatalogService.InvokeToolAsync(toolName, arguments, cancellationToken).ConfigureAwait(false);

        return new StepExecutionState(
            result.Success,
            result.Success ? result.Content : result.Error,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["toolName"] = toolName,
                ["content"] = result.Content,
                ["error"] = result.Error
            });
    }

    private async Task<StepExecutionState> ExecutePluginStepAsync(
        WorkflowStep step,
        ScriptStepDefinition definition,
        AgentExecutionContext context,
        IReadOnlyDictionary<string, StepExecutionState> stepResults,
        CancellationToken cancellationToken)
    {
        var pluginId = ResolveTemplate(definition.Script, context, stepResults);
        var parameters = ResolveArguments(step, context, stepResults, ["pluginId", "userId"]);
        var userId = ResolveArgumentValue(step.Inputs.TryGetValue("userId", out var binding) ? binding : null, context, stepResults)?.ToString();
        var pluginContext = PluginContext.Create(string.IsNullOrWhiteSpace(userId) ? "workflow-bridge" : userId!, parameters);
        var result = await _pluginService.ExecutePluginAsync(pluginId, pluginContext, cancellationToken).ConfigureAwait(false);

        return new StepExecutionState(
            result.IsSuccess,
            result.IsSuccess ? SerializeValue(result.Data) : result.ErrorMessage,
            result.Data.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<StepExecutionState> ExecuteAgentStepAsync(
        WorkflowStep step,
        ScriptStepDefinition definition,
        AgentExecutionContext context,
        IOrchestrationPlan plan,
        IReadOnlyList<ContractSpecialistAgent> candidates,
        IReadOnlyDictionary<string, StepExecutionState> stepResults,
        CancellationToken cancellationToken)
    {
        var requestedAgentName = ResolveTemplate(definition.Script, context, stepResults);
        var prompt = ResolveArgumentValue(step.Inputs.TryGetValue("prompt", out var promptBinding) ? promptBinding : null, context, stepResults)?.ToString();
        var agent = string.IsNullOrWhiteSpace(requestedAgentName)
            ? candidates.FirstOrDefault()
            : candidates.FirstOrDefault(candidate => candidate.Name.Equals(requestedAgentName, StringComparison.OrdinalIgnoreCase));

        if (agent is null)
        {
            return new StepExecutionState(false, $"Workflow-backed orchestration could not resolve agent '{requestedAgentName}'.");
        }

        var mergedContext = new Dictionary<string, string>(context.Task.Context ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
        {
            ["workflowStepKey"] = step.Key,
            ["agentId"] = agent.Name
        };

        var task = context.Task with
        {
            Prompt = string.IsNullOrWhiteSpace(prompt) ? context.Task.Prompt : prompt,
            Context = mergedContext
        };

        var childContext = context.CreateChild(task);
        childContext.SetMaxTurns(context.MaxTurns);
        var result = await _agentCoordinator.ExecuteAsync(childContext, [agent], plan.ResolveStrategy(task), cancellationToken).ConfigureAwait(false);

        return new StepExecutionState(
            result.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) || result.Status.Equals("Success", StringComparison.OrdinalIgnoreCase),
            result.Output,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["agentId"] = result.AgentId,
                ["status"] = result.Status,
                ["output"] = result.Output,
                ["tokensUsed"] = result.TokensUsed,
                ["executionTimeMs"] = result.ExecutionTimeMs
            },
            result.Status,
            result.AgentId,
            result.TokensUsed,
            result.ExecutionTimeMs,
            result.Artifacts);
    }

    private static string DescribeStep(WorkflowStep step)
        => step.Definition switch
        {
            LlmStepDefinition llm => $"llm:{llm.Model}",
            ScriptStepDefinition script => $"{script.Language}:{script.Script}",
            _ => step.Definition.GetType().Name
        };

    private static Dictionary<string, object> ResolveArguments(
        WorkflowStep step,
        AgentExecutionContext context,
        IReadOnlyDictionary<string, StepExecutionState> stepResults,
        IReadOnlyCollection<string> excludedKeys)
    {
        var arguments = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in step.Inputs)
        {
            if (excludedKeys.Contains(input.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = ResolveArgumentValue(input.Value, context, stepResults);
            arguments[input.Key] = value ?? string.Empty;
        }

        return arguments;
    }

    private static object? ResolveArgumentValue(
        InputBinding? binding,
        AgentExecutionContext context,
        IReadOnlyDictionary<string, StepExecutionState> stepResults)
    {
        if (binding is null)
        {
            return null;
        }

        return binding switch
        {
            StaticInputBinding staticBinding => TryDeserializeJson(staticBinding.StaticJson),
            ExpressionInputBinding expressionBinding => ResolveExpression(expressionBinding.Expression, context, stepResults),
            _ => null
        };
    }

    private static object? TryDeserializeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ConvertJsonElement(document.RootElement);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static object? ResolveExpression(
        string expression,
        AgentExecutionContext context,
        IReadOnlyDictionary<string, StepExecutionState> stepResults)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        var segments = expression.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        return segments[0].ToLowerInvariant() switch
        {
            "task" => ResolveTaskExpression(segments, context),
            "steps" => ResolveStepExpression(segments, stepResults),
            _ => expression
        };
    }

    private static object? ResolveTaskExpression(string[] segments, AgentExecutionContext context)
    {
        if (segments.Length < 2)
        {
            return context.Task.Prompt;
        }

        return segments[1].ToLowerInvariant() switch
        {
            "prompt" => context.Task.Prompt,
            "id" => context.Task.Id,
            "type" => context.Task.Type.ToString(),
            "context" when segments.Length >= 3 && context.Task.Context is not null && context.Task.Context.TryGetValue(segments[2], out var value) => value,
            _ => string.Empty
        };
    }

    private static object? ResolveStepExpression(string[] segments, IReadOnlyDictionary<string, StepExecutionState> stepResults)
    {
        if (segments.Length < 2 || !stepResults.TryGetValue(segments[1], out var state))
        {
            return string.Empty;
        }

        if (segments.Length == 2)
        {
            return state.OutputSummary;
        }

        var field = segments[2].ToLowerInvariant();
        if (field == "output")
        {
            return state.OutputSummary;
        }

        if (field == "status")
        {
            return state.Status;
        }

        if (state.Data.TryGetValue(segments[2], out var value))
        {
            return value;
        }

        return string.Empty;
    }

    private static string ResolveTemplate(
        string template,
        AgentExecutionContext context,
        IReadOnlyDictionary<string, StepExecutionState> stepResults)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return TemplateTokenRegex().Replace(template, match =>
        {
            var expression = match.Groups[1].Value;
            var value = ResolveExpression(expression, context, stepResults);
            return value switch
            {
                null => string.Empty,
                string text => text,
                _ => SerializeValue(value)
            };
        });
    }

    private static string SerializeValue(object? value)
        => value switch
        {
            null => string.Empty,
            string text => text,
            _ => JsonSerializer.Serialize(value, SerializerOptions)
        };

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => ConvertJsonElement(property.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDouble(out var floatingPoint) => floatingPoint,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    [GeneratedRegex("\\{\\{\\s*([^}]+?)\\s*\\}\\}", RegexOptions.Compiled)]
    private static partial Regex TemplateTokenRegex();

    private sealed class LocalWorkflowOrchestrationStrategy : IOrchestrationStrategy
    {
        public IReadOnlyList<WorkflowStep> ResolveNextSteps(
            WorkflowOrchestrationContext runContext,
            WorkflowStepExecutionResult? currentStepResult = null)
        {
            ArgumentNullException.ThrowIfNull(runContext);

            var currentStepKey = currentStepResult?.StepKey ?? runContext.CurrentStepKey;
            return string.IsNullOrWhiteSpace(currentStepKey)
                ? ResolveFirstStep(runContext.Definition) is { } firstStep ? [firstStep] : []
                : ResolveNextSteps(runContext.Definition, currentStepKey);
        }

        private static WorkflowStep? ResolveFirstStep(NemFlowDefinition definition)
        {
            if (definition.Steps.Count == 0)
            {
                return null;
            }

            var incoming = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edge in definition.Edges)
            {
                if (!string.IsNullOrWhiteSpace(edge.TargetStepKey))
                {
                    incoming.Add(edge.TargetStepKey);
                }
            }

            foreach (var step in definition.Steps)
            {
                foreach (var nextStepKey in step.NextStepKeys)
                {
                    if (!string.IsNullOrWhiteSpace(nextStepKey))
                    {
                        incoming.Add(nextStepKey);
                    }
                }
            }

            return definition.Steps.FirstOrDefault(step => !incoming.Contains(step.Key))
                ?? definition.Steps[0];
        }

        private static IReadOnlyList<WorkflowStep> ResolveNextSteps(NemFlowDefinition definition, string currentStepKey)
        {
            var currentStep = definition.Steps.FirstOrDefault(step => step.Key.Equals(currentStepKey, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Workflow step '{currentStepKey}' was not found.");

            var nextStepKeys = currentStep.NextStepKeys.Count > 0
                ? currentStep.NextStepKeys
                : definition.Edges
                    .Where(edge => edge.SourceStepKey.Equals(currentStepKey, StringComparison.Ordinal))
                    .Select(edge => edge.TargetStepKey)
                    .ToList();

            if (nextStepKeys.Count == 0)
            {
                return [];
            }

            var stepMap = definition.Steps.ToDictionary(step => step.Key, StringComparer.Ordinal);
            var resolved = new List<WorkflowStep>();
            foreach (var nextStepKey in nextStepKeys.Distinct(StringComparer.Ordinal))
            {
                if (!stepMap.TryGetValue(nextStepKey, out var nextStep))
                {
                    throw new InvalidOperationException($"Next workflow step '{nextStepKey}' was not found.");
                }

                resolved.Add(nextStep);
            }

            return resolved;
        }
    }

    private sealed record StepExecutionState(
        bool IsSuccess,
        string? OutputSummary,
        IReadOnlyDictionary<string, object?>? Data = null,
        string Status = "Completed",
        string AgentId = "workflow-bridge",
        int TokensUsed = 0,
        long ExecutionTimeMs = 0,
        IReadOnlyList<string>? Artifacts = null)
    {
        public IReadOnlyDictionary<string, object?> Data { get; init; } = Data ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        public AgentResult ToAgentResult(string taskId, string defaultAgentId)
            => new(
                taskId,
                string.IsNullOrWhiteSpace(AgentId) ? defaultAgentId : AgentId,
                IsSuccess ? Status : "Failed",
                OutputSummary,
                TokensUsed,
                ExecutionTimeMs,
                Artifacts);
    }
}
