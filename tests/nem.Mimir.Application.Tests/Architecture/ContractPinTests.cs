using nem.Contracts.Identity;
using nem.Contracts.Inference;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Domain.Plugins;
using nem.Mimir.Domain.Tools;

namespace nem.Mimir.Application.Tests.Architecture;

public sealed class ContractPinTests
{
    [Fact]
    public void Service_contract_signatures_are_pinned()
    {
        Func<IPluginService, string, CancellationToken, Task<PluginMetadata>> loadPlugin =
            static (service, assemblyPath, ct) => service.LoadPluginAsync(assemblyPath, ct);
        Func<IPluginService, string, CancellationToken, Task> unloadPlugin =
            static (service, pluginId, ct) => service.UnloadPluginAsync(pluginId, ct);
        Func<IPluginService, CancellationToken, Task<IReadOnlyList<PluginMetadata>>> listPlugins =
            static (service, ct) => service.ListPluginsAsync(ct);
        Func<IPluginService, string, PluginContext, CancellationToken, Task<PluginResult>> executePlugin =
            static (service, pluginId, context, ct) => service.ExecutePluginAsync(pluginId, context, ct);

        Func<IToolProvider, CancellationToken, Task<IReadOnlyList<ToolDefinition>>> listTools =
            static (provider, ct) => provider.ListToolsAsync(ct);
        Func<IToolProvider, string, Dictionary<string, object>, CancellationToken, Task<ToolInvocationResult>> invokeTool =
            static (provider, toolName, arguments, ct) => provider.InvokeToolAsync(toolName, arguments, ct);

        Func<ILlmService, string, IReadOnlyList<LlmMessage>, CancellationToken, Task<LlmResponse>> sendMessage =
            static (service, model, messages, ct) => service.SendMessageAsync(model, messages, ct);
        Func<ILlmService, string, IReadOnlyList<LlmMessage>, CancellationToken, IAsyncEnumerable<LlmStreamChunk>> streamMessage =
            static (service, model, messages, ct) => service.StreamMessageAsync(model, messages, ct);
        Func<ILlmService, CancellationToken, Task<IReadOnlyList<LlmModelInfoDto>>> getModels =
            static (service, ct) => service.GetAvailableModelsAsync(ct);

        Assert.NotNull(loadPlugin);
        Assert.NotNull(unloadPlugin);
        Assert.NotNull(listPlugins);
        Assert.NotNull(executePlugin);
        Assert.NotNull(listTools);
        Assert.NotNull(invokeTool);
        Assert.NotNull(sendMessage);
        Assert.NotNull(streamMessage);
        Assert.NotNull(getModels);
    }

    [Fact]
    public void Local_typed_ids_remain_guid_backed_value_types()
    {
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.ArenaConfigId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.AuditEntryId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.ConversationId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.EvaluationFeedbackId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.EvaluationId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.FolderId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.KnowledgeCollectionId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.LeaderboardEntryId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.MessageId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.ModelProfileId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.SystemPromptId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.UserId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.UserMemoryId>();
        AssertLocalGuidTypedId<nem.Mimir.Domain.ValueObjects.UserPreferenceId>();
    }

    [Fact]
    public void Shared_typed_ids_used_by_mimir_remain_guid_backed_value_types()
    {
        AssertContractsGuidTypedId<CorrelationId>();
        AssertContractsGuidTypedId<SkillId>();
        AssertContractsGuidTypedId<SkillVersionId>();
        AssertContractsGuidTypedId<InferenceProviderId>();
    }

    private static void AssertLocalGuidTypedId<TId>()
        where TId : struct, nem.Mimir.Domain.Common.ITypedId<Guid>
    {
        Assert.True(typeof(TId).IsValueType);
        Assert.Equal(Guid.Empty, default(TId).Value);
    }

    private static void AssertContractsGuidTypedId<TId>()
        where TId : struct, nem.Contracts.Identity.ITypedId<Guid>
    {
        Assert.True(typeof(TId).IsValueType);
        Assert.Equal(Guid.Empty, default(TId).Value);
    }
}
