using System.Net;
using Microsoft.Extensions.Options;
using Mimir.Tui.Commands;
using Mimir.Tui.Models;
using Mimir.Tui.Services;
using Shouldly;

namespace Mimir.Tui.Tests;

/// <summary>
/// Comprehensive negative tests for TUI layer: command parsing edge cases,
/// model service boundary conditions, and service failure modes.
/// </summary>
public sealed class TuiNegativeTests
{
    // ══════════════════════════════════════════════════════════════════
    // CommandParser — edge cases and boundary conditions
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void CommandParser_NullInput_ReturnsChatCommand()
    {
        var result = CommandParser.Parse(null!);

        result.Type.ShouldBe(CommandType.Chat);
    }

    [Fact]
    public void CommandParser_SlashOnly_ReturnsChatCommand()
    {
        var result = CommandParser.Parse("/");

        result.Type.ShouldBe(CommandType.Chat);
    }

    [Fact]
    public void CommandParser_VeryLongInput_ReturnsChatCommand()
    {
        var longInput = new string('a', 100_000);
        var result = CommandParser.Parse(longInput);

        result.Type.ShouldBe(CommandType.Chat);
        result.Argument.ShouldBe(longInput);
    }

    [Fact]
    public void CommandParser_SpecialCharacters_ReturnsChatCommand()
    {
        var result = CommandParser.Parse("!@#$%^&*()");

        result.Type.ShouldBe(CommandType.Chat);
        result.Argument.ShouldBe("!@#$%^&*()");
    }

    [Fact]
    public void CommandParser_MultipleSlashCommands_OnlyParsesFirst()
    {
        var result = CommandParser.Parse("/help /model phi-4");

        result.Type.ShouldBe(CommandType.Help);
    }

    // ══════════════════════════════════════════════════════════════════
    // ModelService — negative and boundary conditions
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void ModelService_TrySetModel_EmptyString_ReturnsFalse()
    {
        var authService = CreateAuthService();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, "[]"));
        var service = new ModelService(httpClient, authService, Options.Create(CreateSettings()));

        var result = service.TrySetModel("");

        result.ShouldBeFalse();
    }

    [Fact]
    public void ModelService_TrySetModel_NullModel_ReturnsFalse()
    {
        var authService = CreateAuthService();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, "[]"));
        var service = new ModelService(httpClient, authService, Options.Create(CreateSettings()));

        // Null should return false (not throw)
        var result = service.TrySetModel(null!);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ModelService_IsKnownModel_EmptyString_ReturnsFalse()
    {
        ModelService.IsKnownModel("").ShouldBeFalse();
    }

    [Fact]
    public void ModelService_IsKnownModel_NullString_ReturnsFalse()
    {
        ModelService.IsKnownModel(null!).ShouldBeFalse();
    }

    [Fact]
    public void ModelService_IsKnownModel_CaseSensitive_ReturnsFalse()
    {
        // Known model names are case-sensitive
        ModelService.IsKnownModel("PHI-4-MINI").ShouldBeFalse();
    }

    [Fact]
    public async Task ModelService_FetchModels_ServerError_ReturnsEmptyList()
    {
        var authService = CreateAuthService();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, ""));
        var service = new ModelService(httpClient, authService, Options.Create(CreateSettings()));

        var models = await service.GetModelsAsync();

        models.ShouldBeEmpty();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static TuiSettings CreateSettings(string defaultModel = "phi-4-mini") => new()
    {
        DefaultModel = defaultModel,
    };

    private static AuthenticationService CreateAuthService()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler);
        return new AuthenticationService(httpClient, Options.Create(new TuiSettings()));
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
