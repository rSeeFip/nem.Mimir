using Microsoft.Extensions.Options;
using nem.Mimir.Application.Mcp;
using nem.Mimir.Domain.Tools;
using Shouldly;

namespace nem.Mimir.Application.Tests.Mcp;

public sealed class LumePersonaFilterTests
{
    [Fact]
    public async Task LumePersona_IncludesLumeTools()
    {
        var sut = CreateSut();
        var tools = CreateTools();

        var result = await sut.FilterAsync(tools, "lume", CancellationToken.None);

        result.ShouldContain(tool => tool.Name == "lume_create_task");
        result.ShouldContain(tool => tool.Name == "lume_list_projects");
    }

    [Fact]
    public async Task LumePersona_ExcludesFinanceOnlyTools()
    {
        var sut = CreateSut();
        var tools = CreateTools();

        var result = await sut.FilterAsync(tools, "lume", CancellationToken.None);

        result.ShouldNotContain(tool => tool.Name == "get_stock_price");
        result.ShouldNotContain(tool => tool.Name == "portfolio_analysis");
    }

    [Fact]
    public async Task LumePersona_IncludesGeneralTools()
    {
        var sut = CreateSut();
        var tools = CreateTools();

        var result = await sut.FilterAsync(tools, "lume", CancellationToken.None);

        result.ShouldContain(tool => tool.Name == "search_documents");
        result.ShouldContain(tool => tool.Name == "notifications_list");
    }

    [Fact]
    public async Task UnknownPersona_ReturnsAllTools()
    {
        var sut = CreateSut();
        var tools = CreateTools();

        var result = await sut.FilterAsync(tools, "unknown", CancellationToken.None);

        result.Count.ShouldBe(tools.Count);
    }

    private static LumePersonaToolFilter CreateSut()
    {
        var options = Options.Create(new PersonaToolFilterOptions
        {
            Personas =
            {
                ["lume"] = new PersonaConfig
                {
                    IncludedServerNames = ["lume-api"],
                    IncludedToolNamePrefixes = ["lume_"],
                    ExcludedToolNames =
                    [
                        "get_stock_price",
                        "analyze_sentiment",
                        "get_prediction",
                        "screen_stocks",
                        "portfolio_analysis",
                    ],
                }
            }
        });

        return new LumePersonaToolFilter(options);
    }

    private static IReadOnlyList<ToolDefinition> CreateTools()
    {
        return new List<ToolDefinition>
        {
            new("lume_create_task", "Create task", "lume-api", null),
            new("lume_list_projects", "List projects", "lume-api", null),
            new("search_documents", "Search docs", "knowhub", null),
            new("notifications_list", "List notifications", "core-api", null),
            new("get_stock_price", "Get stock price", "finance-api", null),
            new("portfolio_analysis", "Analyze portfolio", "finance-api", null),
        }.AsReadOnly();
    }
}
