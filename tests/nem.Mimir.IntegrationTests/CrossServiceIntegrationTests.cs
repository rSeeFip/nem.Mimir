using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mimir.Application.Agents;
using Mimir.Application.CodeExecution.Commands;
using Mimir.Application.Common.Interfaces;
using Mimir.Application.Common.Models;
using Mimir.Application.Context;
using Mimir.Application.Knowledge;
using Mimir.Application.Llm;
using Mimir.Application.Services.Memory;
using Mimir.Application.Tasks;
using Mimir.Application.Tokens;
using Mimir.Domain.Entities;
using Mimir.Infrastructure.Cache;
using Mimir.Infrastructure.Mcp;
using NSubstitute;
using Shouldly;
using nem.Contracts.Agents;
using nem.Contracts.Memory;
using nem.Contracts.TokenOptimization;

namespace nem.Mimir.IntegrationTests;

public sealed class CrossServiceIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListToolsAsync_WhenAgentRequestsToolDiscovery_ShouldReturnToolRegistryResponse()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(request =>
        {
            request.RequestUri!.AbsolutePath.ShouldBe("/tools/list");

            const string payload = """
                                   {
                                     "tools": [
                                       {
                                         "name": "searchDocs",
                                         "description": "Search docs",
                                         "inputSchema": { "type": "object" }
                                       }
                                     ]
                                   }
                                   """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        });

        var manager = CreateMcpManager(handler);

        var provider = new McpToolAdapter(manager);

        // Act
        var tools = await provider.ListToolsAsync();

        // Assert
        tools.Count.ShouldBe(1);
        tools[0].Name.ShouldBe("searchDocs");
        tools[0].ServerName.ShouldBe("mcp-main");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task InvokeToolAsync_WhenAgentExecutesRegisteredTool_ShouldReturnToolExecutionResult()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/tools/list")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "tools": [ { "name": "searchDocs", "description": "desc" } ] }""", Encoding.UTF8, "application/json")
                });
            }

            request.RequestUri!.AbsolutePath.ShouldBe("/tools/call");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "isError": false, "content": "3 documents found" }""", Encoding.UTF8, "application/json")
            });
        });

        var manager = CreateMcpManager(handler);

        var provider = new McpToolAdapter(manager);

        // Act
        var result = await provider.InvokeToolAsync("mcp-main.searchDocs", new Dictionary<string, object> { ["query"] = "mimir" });

        // Assert
        result.Success.ShouldBeTrue();
        result.Content.ShouldBe("3 documents found");
        result.Error.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DispatchAsync_WhenOrchestratorDelegatesToSpecialist_ShouldCompleteDelegationChain()
    {
        // Arrange
        var llm = Substitute.For<ILlmService>();
        var dispatcher = new AgentDispatcher([
            new SpecialistAgentStub(
                "execute-specialist",
                [AgentCapability.CodeExecution],
                task => task.Type == AgentTaskType.Execute,
                task => new AgentResult(task.Id, "execute-specialist", "Completed", "delegated execution complete"))
        ]);
        var coordinator = new AgentCoordinator(llm);
        var orchestrator = new AgentOrchestrator(dispatcher, coordinator, llm);

        var task = new AgentTask(
            "delegation-1",
            AgentTaskType.Execute,
            "compile service",
            new Dictionary<string, string> { ["strategy"] = "sequential" });

        // Act
        var result = await orchestrator.DispatchAsync(task);

        // Assert
        result.Status.ShouldBe("Completed");
        result.AgentId.ShouldBe("execute-specialist");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConsolidateAsync_WhenWorkingMemoryHasConversationContext_ShouldPersistToEpisodicMemory()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var workingMemory = Substitute.For<IWorkingMemory>();
        workingMemory.GetRecentAsync("conv-1", 20, Arg.Any<CancellationToken>())
            .Returns([
                new ConversationMessage("user", "important context", DateTimeOffset.UtcNow, new Dictionary<string, string> { ["userId"] = userId })
            ]);

        var episodicMemory = Substitute.For<IEpisodicMemory>();
        episodicMemory.GetEpisodesAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Episode>());

        var semanticMemory = Substitute.For<ISemanticMemory>();
        var knowHub = Substitute.For<IKnowHubBridge>();
        knowHub.IsAvailable.Returns(false);

        var sut = new MemoryConsolidationService(
            workingMemory,
            episodicMemory,
            semanticMemory,
            knowHub,
            new MemoryConsolidationOptions(),
            Substitute.For<ILogger<MemoryConsolidationService>>());

        // Act
        await sut.ConsolidateAsync("conv-1");

        // Assert
        await episodicMemory.Received(1).SummarizeSessionAsync("conv-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConsolidateAsync_WhenEpisodicFactsAreDistilled_ShouldStoreSemanticMemoryFacts()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var episode = new Episode(
            "episode-1",
            userId,
            "conv-1",
            "critical deployment issue",
            ["critical", "deployment"],
            DateTimeOffset.UtcNow.AddMinutes(-15),
            DateTimeOffset.UtcNow,
            6);

        var workingMemory = Substitute.For<IWorkingMemory>();
        workingMemory.GetRecentAsync("conv-1", 20, Arg.Any<CancellationToken>())
            .Returns([
                new ConversationMessage("user", "context", DateTimeOffset.UtcNow, new Dictionary<string, string> { ["userId"] = userId })
            ]);

        var episodicMemory = Substitute.For<IEpisodicMemory>();
        episodicMemory.GetEpisodesAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([episode]);

        var semanticMemory = Substitute.For<ISemanticMemory>();
        var knowHub = Substitute.For<IKnowHubBridge>();
        knowHub.IsAvailable.Returns(true);
        knowHub.DistillKnowledgeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([
                new DistilledKnowledge(Guid.NewGuid(), "use canary rollout", 0.9, "decision", "canary rollout")
            ]);

        var sut = new MemoryConsolidationService(
            workingMemory,
            episodicMemory,
            semanticMemory,
            knowHub,
            new MemoryConsolidationOptions { EpisodicToSemanticThreshold = 0.6f },
            Substitute.For<ILogger<MemoryConsolidationService>>());

        // Act
        await sut.ConsolidateAsync("conv-1", MemoryConsolidationStrategy.Balanced);

        // Assert
        await semanticMemory.Received(1).StoreFactAsync(
            Arg.Is<KnowledgeFact>(f => f.Predicate == "decision" && f.Object.Contains("canary", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAsync_WhenSemanticallySimilarPromptExists_ShouldReturnSemanticCacheHit()
    {
        // Arrange
        var sut = CreateSemanticCache(new SemanticCacheOptions { EnablePerUserIsolation = false });

        await sut.SetAsync("How to optimize context windows", "Use pruning and distillation");

        // Act
        var result = await sut.GetAsync("how to optimize context window", 0.90f);

        // Assert
        result.ShouldBe("Use pruning and distillation");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAsync_WhenPromptIsNovel_ShouldReturnSemanticCacheMiss()
    {
        // Arrange
        var sut = CreateSemanticCache(new SemanticCacheOptions { EnablePerUserIsolation = false });

        await sut.SetAsync("deploy kubernetes manifests", "kustomize apply");

        // Act
        var result = await sut.GetAsync("explain renaissance art", 0.98f);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SelectModelAsync_WhenFastTierCannotHandleContext_ShouldEscalateModelCascade()
    {
        // Arrange
        var sut = new ModelCascadeService(
            new ModelCascadeOptions { DefaultTier = ModelTier.Fast },
            Substitute.For<ILogger<ModelCascadeService>>());

        // Act
        var selected = await sut.SelectModelAsync(new ModelSelectionContext("analysis", 1500, 200, RequiresReasoning: false));

        // Assert
        selected.Tier.ShouldBe(ModelTier.Standard);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetUsageAsync_WhenMultipleServiceEventsRecorded_ShouldTrackTokenBudgetAcrossConversation()
    {
        // Arrange
        var sut = new TokenTrackerService(
            new TokenTrackerOptions { EnableTracking = true },
            Substitute.For<ILogger<TokenTrackerService>>());

        var from = DateTimeOffset.UtcNow.AddMinutes(-2);
        var to = DateTimeOffset.UtcNow.AddMinutes(2);

        // Act
        await sut.RecordUsageAsync(new TokenUsageEvent("agent-service", "gpt-4o-mini", 120, 80, 0.01m, DateTimeOffset.UtcNow));
        await sut.RecordUsageAsync(new TokenUsageEvent("agent-service", "gpt-4o", 300, 140, 0.07m, DateTimeOffset.UtcNow));
        var usage = await sut.GetUsageAsync("agent-service", from, to);

        // Assert
        usage.RequestCount.ShouldBe(2);
        usage.TotalInputTokens.ShouldBe(420);
        usage.TotalOutputTokens.ShouldBe(220);
        usage.TotalCost.ShouldBe(0.08m);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PruneAsync_WhenContextCrossesEightyPercentThreshold_ShouldTriggerContextOptimization()
    {
        // Arrange
        var options = new ContextOptimizerOptions
        {
            MaxContextTokens = 100,
            AutoTriggerThreshold = 0.8,
            TargetCompressionRatio = 0.5,
            CharsPerToken = 1
        };
        var sut = new ContextOptimizerService(options, Substitute.For<ILogger<ContextOptimizerService>>());
        var content = string.Join("\n\n", Enumerable.Range(1, 8).Select(i => $"user: message-{i} {new string('x', 20)}"));

        // Act
        var optimized = await sut.PruneAsync(content, PruneStrategy.Summarize);

        // Assert
        sut.EstimateTokens(content).ShouldBeGreaterThanOrEqualTo(80);
        sut.EstimateTokens(optimized).ShouldBeLessThanOrEqualTo(sut.EstimateTokens(content));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessTaskAsync_WhenBackgroundTaskSubmittedAndExecuted_ShouldCompleteLifecycleAndExposeResult()
    {
        // Arrange
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var runtime = Substitute.For<IBackgroundTaskRuntime>();
        runtime.DispatchAsync(Arg.Any<AgentTask>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var task = call.Arg<AgentTask>();
                return Task.FromResult(new AgentResult(task.Id, "specialist", BackgroundTaskStatus.Completed, "done"));
            });

        var queue = System.Threading.Channels.Channel.CreateUnbounded<BackgroundTaskItem>();
        var sut = new BackgroundTaskExecutor(
            cache,
            runtime,
            queue,
            Options.Create(new BackgroundTaskOptions()),
            Substitute.For<ILogger<BackgroundTaskExecutor>>());

        var task = new AgentTask("bg-1", AgentTaskType.Analyze, "analyze service interactions");

        // Act
        var taskId = await sut.SubmitAsync(task);
        var queuedItem = await queue.Reader.ReadAsync();
        await sut.ProcessTaskAsync(queuedItem, CancellationToken.None);
        var result = await sut.GetResultAsync(taskId);

        // Assert
        result.ShouldNotBeNull();
        result!.Status.ShouldBe(BackgroundTaskStatus.Completed);
        result.Output.ShouldBe("done");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Handle_WhenSandboxCodeExecutionRequested_ShouldValidateExecuteAndReturnResult()
    {
        // Arrange
        var repository = Substitute.For<IConversationRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var sandbox = Substitute.For<ISandboxService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var userId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());
        currentUser.Roles.Returns(Array.Empty<string>());

        var conversation = Conversation.Create(userId, "sandbox session");
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(conversation);

        sandbox.ExecuteAsync("print('hi')", "python", Arg.Any<CancellationToken>())
            .Returns(new CodeExecutionResult("hi", string.Empty, 0, 15, false));

        var handler = CreateExecuteCodeHandler(repository, currentUser, sandbox, unitOfWork);
        var command = new ExecuteCodeCommand("python", "print('hi')", conversation.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Stdout.ShouldBe("hi");
        await sandbox.Received(1).ExecuteAsync("print('hi')", "python", Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static MediatR.IRequestHandler<ExecuteCodeCommand, CodeExecutionResultDto> CreateExecuteCodeHandler(
        IConversationRepository conversationRepository,
        ICurrentUserService currentUserService,
        ISandboxService sandboxService,
        IUnitOfWork unitOfWork)
    {
        var handlerType = typeof(ExecuteCodeCommand).Assembly
            .GetTypes()
            .Single(t => t.Name == "ExecuteCodeCommandHandler");

        return (MediatR.IRequestHandler<ExecuteCodeCommand, CodeExecutionResultDto>)Activator.CreateInstance(
            handlerType,
            conversationRepository,
            currentUserService,
            sandboxService,
            unitOfWork)!;
    }

    private static McpClientManager CreateMcpManager(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpServers:0:Name"] = "mcp-main",
                ["McpServers:0:Url"] = "https://mcp.local",
                ["McpServers:0:RequiresAuth"] = "false"
            })
            .Build();

        return new McpClientManager(
            new StubHttpClientFactory(new HttpClient(handler)),
            configuration,
            new HttpContextAccessor(),
            TimeProvider.System,
            Substitute.For<ILogger<McpClientManager>>());
    }

    private static ISemanticCache CreateSemanticCache(SemanticCacheOptions options)
    {
        var assembly = typeof(global::Mimir.Infrastructure.DependencyInjection).Assembly;
        var cacheType = assembly.GetType("Mimir.Infrastructure.Cache.SemanticCacheService", throwOnError: true)!;
        var loggerInterfaceType = typeof(ILogger<>).MakeGenericType(cacheType);
        var logger = Substitute.For([loggerInterfaceType], Array.Empty<object>());
        var constructor = cacheType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        var instance = constructor.Invoke([options, logger, null]);
        return (ISemanticCache)instance;
    }

    private sealed class SpecialistAgentStub : nem.Contracts.Agents.ISpecialistAgent
    {
        private readonly Func<AgentTask, bool> _canHandle;
        private readonly Func<AgentTask, AgentResult> _execute;

        public SpecialistAgentStub(
            string name,
            IReadOnlyList<AgentCapability> capabilities,
            Func<AgentTask, bool> canHandle,
            Func<AgentTask, AgentResult> execute)
        {
            Name = name;
            Capabilities = capabilities;
            _canHandle = canHandle;
            _execute = execute;
        }

        public string Name { get; }

        public string Description => $"{Name} specialist";

        public IReadOnlyList<AgentCapability> Capabilities { get; }

        public Task<AgentResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
            => Task.FromResult(_execute(task));

        public Task<bool> CanHandleAsync(AgentTask task, CancellationToken cancellationToken = default)
            => Task.FromResult(_canHandle(task));
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => responder(request);
    }
}
