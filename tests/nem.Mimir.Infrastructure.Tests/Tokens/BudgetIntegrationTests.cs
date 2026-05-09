namespace nem.Mimir.Infrastructure.Tests.Tokens;

using Microsoft.Extensions.Logging;
using nem.Contracts.TokenOptimization;
using nem.Mimir.Application.Common.Interfaces;
using nem.Mimir.Application.Common.Models;
using nem.Mimir.Infrastructure.Tokens;
using NSubstitute;
using Shouldly;

public sealed class BudgetIntegrationTests
{
    [Fact]
    public async Task SendMessageAsync_UnderBudget_ProceedsAfterBudgetCheck()
    {
        var inner = Substitute.For<ILlmService>();
        var policy = Substitute.For<ITokenBudgetPolicy>();
        var decorator = CreateDecorator(inner, policy);
        var messages = CreateMessages();
        var response = new LlmResponse("allowed", "gpt-4o", 12, 8, 20, "stop");

        policy.CheckBudgetAsync(Arg.Any<TokenBudgetContext>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                inner.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default);
                return new BudgetCheckResult(BudgetAction.Allow, 100m, 500m, null);
            });
        inner.SendMessageAsync("gpt-4o", messages, Arg.Any<CancellationToken>())
            .Returns(response);

        var result = await decorator.SendMessageAsync("gpt-4o", messages, CancellationToken.None);

        result.ShouldBe(response);
        await policy.Received(1).CheckBudgetAsync(
            Arg.Is<TokenBudgetContext>(context =>
                context.ServiceId == "nem.mimir"
                && context.ModelId == "gpt-4o"
                && context.InputTokens > 0
                && context.OutputTokens == 0),
            Arg.Any<CancellationToken>());
        await inner.Received(1).SendMessageAsync("gpt-4o", messages, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_NearBudget_LogsWarningButProceeds()
    {
        var inner = Substitute.For<ILlmService>();
        var policy = Substitute.For<ITokenBudgetPolicy>();
        var logger = Substitute.For<ILogger<BudgetLlmServiceDecorator>>();
        var decorator = CreateDecorator(inner, policy, logger);
        var messages = CreateMessages();
        var response = new LlmResponse("warned", "gpt-4o", 10, 5, 15, "stop");

        policy.CheckBudgetAsync(Arg.Any<TokenBudgetContext>(), Arg.Any<CancellationToken>())
            .Returns(new BudgetCheckResult(BudgetAction.Warn, 450m, 500m, "approaching limit"));
        inner.SendMessageAsync("gpt-4o", messages, Arg.Any<CancellationToken>())
            .Returns(response);

        var result = await decorator.SendMessageAsync("gpt-4o", messages, CancellationToken.None);

        result.ShouldBe(response);
        await inner.Received(1).SendMessageAsync("gpt-4o", messages, Arg.Any<CancellationToken>());
        logger.ReceivedCalls()
            .Any(call => call.GetArguments().Length > 0
                         && call.GetArguments()[0] is LogLevel level
                         && level == LogLevel.Warning)
            .ShouldBeTrue();
    }

    [Fact]
    public async Task SendMessageAsync_OverBudget_DeniesWithoutCallingInner()
    {
        var inner = Substitute.For<ILlmService>();
        var policy = Substitute.For<ITokenBudgetPolicy>();
        var decorator = CreateDecorator(inner, policy);

        policy.CheckBudgetAsync(Arg.Any<TokenBudgetContext>(), Arg.Any<CancellationToken>())
            .Returns(new BudgetCheckResult(BudgetAction.Deny, 520m, 500m, "budget exceeded"));

        var result = await decorator.SendMessageAsync("gpt-4o", CreateMessages(), CancellationToken.None);

        result.Content.ShouldBe("budget exceeded");
        result.Model.ShouldBe("gpt-4o");
        result.FinishReason.ShouldBe("budget_denied");
        await inner.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default);
        await policy.DidNotReceive().RecordUsageAsync(Arg.Any<TokenUsage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_Success_RecordsUsageAfterInnerCall()
    {
        var inner = Substitute.For<ILlmService>();
        var policy = Substitute.For<ITokenBudgetPolicy>();
        var decorator = CreateDecorator(inner, policy);
        var response = new LlmResponse("done", "gpt-4o-mini", 21, 34, 55, "stop");

        policy.CheckBudgetAsync(Arg.Any<TokenBudgetContext>(), Arg.Any<CancellationToken>())
            .Returns(new BudgetCheckResult(BudgetAction.Allow, 120m, 500m, null));
        inner.SendMessageAsync("gpt-4o", Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var before = DateTimeOffset.UtcNow;

        await decorator.SendMessageAsync("gpt-4o", CreateMessages(), CancellationToken.None);

        var after = DateTimeOffset.UtcNow;

        Received.InOrder(async () =>
        {
            await policy.CheckBudgetAsync(Arg.Any<TokenBudgetContext>(), Arg.Any<CancellationToken>());
            await inner.SendMessageAsync("gpt-4o", Arg.Any<IReadOnlyList<LlmMessage>>(), Arg.Any<CancellationToken>());
            await policy.RecordUsageAsync(
                Arg.Is<TokenUsage>(usage =>
                    usage.ServiceId == "nem.mimir"
                    && usage.ModelId == "gpt-4o-mini"
                    && usage.InputTokens == 21
                    && usage.OutputTokens == 34
                    && usage.Cost == 0m
                    && usage.Timestamp >= before
                    && usage.Timestamp <= after),
                Arg.Any<CancellationToken>());
        });
    }

    private static BudgetLlmServiceDecorator CreateDecorator(
        ILlmService inner,
        ITokenBudgetPolicy policy,
        ILogger<BudgetLlmServiceDecorator>? logger = null)
        => new(inner, policy, logger: logger);

    private static IReadOnlyList<LlmMessage> CreateMessages()
        => [new LlmMessage("system", "Be concise."), new LlmMessage("user", "hello")];
}
