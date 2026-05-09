using nem.Lume.McpTools;
using Shouldly;

namespace nem.Lume.McpTools.Tests;

public sealed class LumeMcpToolRegistryTests
{
    [Fact]
    public void GetTools_ReturnsFiveExpectedTools()
    {
        var tools = LumeMcpToolRegistry.GetTools();

        tools.Count.ShouldBe(5);
        tools.Select(t => t.Name).ShouldBe(
        [
            "lume_create_task",
            "lume_list_projects",
            "lume_search_knowledge",
            "lume_get_document",
            "lume_schedule_event",
        ]);
    }

    [Fact]
    public void GetTools_AllHaveDescriptionsAndActionMappings()
    {
        var tools = LumeMcpToolRegistry.GetTools();

        foreach (var tool in tools)
        {
            tool.Description.ShouldNotBeNullOrWhiteSpace();
            tool.Action.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void GetTools_AllHaveObjectInputSchema()
    {
        var tools = LumeMcpToolRegistry.GetTools();

        foreach (var tool in tools)
        {
            tool.InputSchema.RootElement.GetProperty("type").GetString().ShouldBe("object");
        }
    }

    [Fact]
    public void GetTools_AllHaveObjectOutputSchema()
    {
        var tools = LumeMcpToolRegistry.GetTools();

        foreach (var tool in tools)
        {
            tool.OutputSchema.RootElement.GetProperty("type").GetString().ShouldBe("object");
            tool.OutputSchema.RootElement.GetProperty("properties").TryGetProperty("status", out _).ShouldBeTrue();
            tool.OutputSchema.RootElement.GetProperty("properties").TryGetProperty("data", out _).ShouldBeTrue();
        }
    }

    [Fact]
    public void LumeMcpTool_CreateTask_InputSchema_RequiresWorkspaceBoardColumnAndTitle()
    {
        var tool = LumeMcpToolRegistry.GetTools().Single(t => t.Name == "lume_create_task");

        var required = tool.InputSchema.RootElement.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        required.ShouldContain("workspaceId");
        required.ShouldContain("boardId");
        required.ShouldContain("columnId");
        required.ShouldContain("title");
    }

    [Fact]
    public void LumeMcpTool_ListProjects_OutputSchema_ContainsProjectsArray()
    {
        var tool = LumeMcpToolRegistry.GetTools().Single(t => t.Name == "lume_list_projects");

        var dataProps = tool.OutputSchema.RootElement
            .GetProperty("properties")
            .GetProperty("data")
            .GetProperty("properties");

        dataProps.TryGetProperty("projects", out _).ShouldBeTrue();
    }

    [Fact]
    public void LumeMcpTool_SearchKnowledge_InputSchema_RequiresWorkspaceAndQuery()
    {
        var tool = LumeMcpToolRegistry.GetTools().Single(t => t.Name == "lume_search_knowledge");

        var required = tool.InputSchema.RootElement.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();
        required.ShouldContain("workspaceId");
        required.ShouldContain("query");
    }

    [Fact]
    public void LumeMcpTool_GetDocument_OutputSchema_ContainsDocumentObject()
    {
        var tool = LumeMcpToolRegistry.GetTools().Single(t => t.Name == "lume_get_document");

        var dataProps = tool.OutputSchema.RootElement
            .GetProperty("properties")
            .GetProperty("data")
            .GetProperty("properties");

        dataProps.TryGetProperty("document", out _).ShouldBeTrue();
    }
}
