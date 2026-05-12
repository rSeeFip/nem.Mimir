using nem.Mimir.Finance.McpTools;
using Shouldly;

namespace nem.Mimir.Finance.McpTools.Tests;

public sealed class FinanceMcpToolRegistryTests
{
    [Fact]
    public void GetTools_ReturnsFiveExpectedTools()
    {
        var tools = FinanceMcpToolRegistry.GetTools();

        tools.Count.ShouldBe(5);
        tools.Select(t => t.Name).ShouldBe(
        [
            "get_stock_price",
            "analyze_sentiment",
            "get_prediction",
            "screen_stocks",
            "portfolio_analysis",
        ]);
    }

    [Fact]
    public void GetTools_AllHaveDescriptionsAndActionMappings()
    {
        var tools = FinanceMcpToolRegistry.GetTools();

        foreach (var tool in tools)
        {
            tool.Description.ShouldNotBeNullOrWhiteSpace();
            tool.Action.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void GetTools_AllHaveObjectInputSchema()
    {
        var tools = FinanceMcpToolRegistry.GetTools();

        foreach (var tool in tools)
        {
            tool.InputSchema.RootElement.GetProperty("type").GetString().ShouldBe("object");
        }
    }

    [Fact]
    public void GetTools_AllHaveObjectOutputSchema()
    {
        var tools = FinanceMcpToolRegistry.GetTools();

        foreach (var tool in tools)
        {
            tool.OutputSchema.RootElement.GetProperty("type").GetString().ShouldBe("object");
            tool.OutputSchema.RootElement.GetProperty("properties").TryGetProperty("status", out _).ShouldBeTrue();
            tool.OutputSchema.RootElement.GetProperty("properties").TryGetProperty("data", out _).ShouldBeTrue();
        }
    }

    [Fact]
    public void GetStockPrice_InputSchema_RequiresTicker()
    {
        var tool = FinanceMcpToolRegistry.GetTools().Single(t => t.Name == "get_stock_price");

        var required = tool.InputSchema.RootElement.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        required.ShouldContain("ticker");
    }

    [Fact]
    public void AnalyzeSentiment_OutputSchema_ContainsPolarityAndConfidence()
    {
        var tool = FinanceMcpToolRegistry.GetTools().Single(t => t.Name == "analyze_sentiment");

        var dataProps = tool.OutputSchema.RootElement
            .GetProperty("properties")
            .GetProperty("data")
            .GetProperty("properties");

        dataProps.TryGetProperty("polarity", out _).ShouldBeTrue();
        dataProps.TryGetProperty("confidence", out _).ShouldBeTrue();
        dataProps.TryGetProperty("score", out _).ShouldBeTrue();
    }

    [Fact]
    public void ScreenStocks_InputSchema_RequiresStocksDataAndFilters()
    {
        var tool = FinanceMcpToolRegistry.GetTools().Single(t => t.Name == "screen_stocks");

        var required = tool.InputSchema.RootElement.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        required.ShouldContain("stocks_data");
        required.ShouldContain("filters");
    }

    [Fact]
    public void PortfolioAnalysis_InputSchema_RequiresHoldings()
    {
        var tool = FinanceMcpToolRegistry.GetTools().Single(t => t.Name == "portfolio_analysis");

        var required = tool.InputSchema.RootElement.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        required.ShouldContain("holdings");
    }
}
