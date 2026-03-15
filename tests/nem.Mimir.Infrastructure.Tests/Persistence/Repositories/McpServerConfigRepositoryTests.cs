namespace nem.Mimir.Infrastructure.Tests.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using nem.Mimir.Domain.McpServers;
using nem.Mimir.Infrastructure.Persistence;
using nem.Mimir.Infrastructure.Persistence.Repositories;
using Shouldly;

public class McpServerConfigRepositoryTests : RepositoryTestBase
{
    private McpServerConfigRepository CreateRepository() => new(Context);

    private McpServerConfigRepository CreateRepository(MimirDbContext context) => new(context);

    private McpServerConfig CreateConfig(
        string name = "Test Server",
        bool isEnabled = true,
        bool isBundled = false)
    {
        return new McpServerConfig
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"{name} description",
            TransportType = McpTransportType.Stdio,
            Command = "npx",
            Arguments = "-y @test/mcp",
            IsEnabled = isEnabled,
            IsBundled = isBundled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task AddAsync_persists_config()
    {
        // Arrange
        var config = CreateConfig("Add Test");
        var repo = CreateRepository();

        // Act
        await repo.AddAsync(config);
        await Context.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext();
        var retrieved = await readContext.McpServerConfigs.FindAsync(config.Id);
        retrieved.ShouldNotBeNull();
        retrieved.Name.ShouldBe("Add Test");
        retrieved.TransportType.ShouldBe(McpTransportType.Stdio);
        retrieved.Command.ShouldBe("npx");
    }

    [Fact]
    public async Task GetByIdAsync_returns_config_with_tool_whitelists()
    {
        // Arrange
        var config = CreateConfig("ById Test");
        config.ToolWhitelists.Add(new McpToolWhitelist
        {
            Id = Guid.NewGuid(),
            McpServerConfigId = config.Id,
            ToolName = "search",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
        });
        Context.McpServerConfigs.Add(config);
        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var result = await repo.GetByIdAsync(config.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("ById Test");
        result.ToolWhitelists.Count.ShouldBe(1);
        result.ToolWhitelists.First().ToolName.ShouldBe("search");
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_not_found()
    {
        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        var result = await repo.GetByIdAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAllAsync_returns_all_configs_ordered_by_name()
    {
        // Arrange
        Context.McpServerConfigs.Add(CreateConfig("Zebra Server"));
        Context.McpServerConfigs.Add(CreateConfig("Alpha Server"));
        Context.McpServerConfigs.Add(CreateConfig("Middle Server", isEnabled: false));
        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var results = await repo.GetAllAsync();

        // Assert
        results.Count.ShouldBeGreaterThanOrEqualTo(3);
        var testConfigs = results.Where(c => !c.IsBundled).ToList();
        testConfigs.Count.ShouldBe(3);
        testConfigs[0].Name.ShouldBe("Alpha Server");
        testConfigs[1].Name.ShouldBe("Middle Server");
        testConfigs[2].Name.ShouldBe("Zebra Server");
    }

    [Fact]
    public async Task GetEnabledAsync_filters_only_enabled_configs()
    {
        // Arrange
        Context.McpServerConfigs.Add(CreateConfig("Enabled One", isEnabled: true));
        Context.McpServerConfigs.Add(CreateConfig("Disabled One", isEnabled: false));
        Context.McpServerConfigs.Add(CreateConfig("Enabled Two", isEnabled: true));
        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var results = await repo.GetEnabledAsync();

        // Assert
        var testConfigs = results.Where(c => !c.IsBundled).ToList();
        testConfigs.Count.ShouldBe(2);
        testConfigs.ShouldAllBe(c => c.IsEnabled);
        testConfigs.ShouldContain(c => c.Name == "Enabled One");
        testConfigs.ShouldContain(c => c.Name == "Enabled Two");
    }

    [Fact]
    public async Task UpdateAsync_modifies_existing_config()
    {
        // Arrange
        var config = CreateConfig("Original Name");
        Context.McpServerConfigs.Add(config);
        await Context.SaveChangesAsync();

        // Act
        await using var updateContext = CreateContext();
        var toUpdate = await updateContext.McpServerConfigs.FindAsync(config.Id);
        toUpdate.ShouldNotBeNull();
        toUpdate.Name = "Updated Name";
        toUpdate.IsEnabled = false;

        var repo = CreateRepository(updateContext);
        await repo.UpdateAsync(toUpdate);
        await updateContext.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext();
        var retrieved = await readContext.McpServerConfigs.FindAsync(config.Id);
        retrieved.ShouldNotBeNull();
        retrieved.Name.ShouldBe("Updated Name");
        retrieved.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_removes_config_and_cascades_to_whitelists()
    {
        // Arrange
        var config = CreateConfig("To Delete");
        config.ToolWhitelists.Add(new McpToolWhitelist
        {
            Id = Guid.NewGuid(),
            McpServerConfigId = config.Id,
            ToolName = "will-be-deleted",
            CreatedAt = DateTime.UtcNow,
        });
        Context.McpServerConfigs.Add(config);
        await Context.SaveChangesAsync();

        // Act
        await using var deleteContext = CreateContext();
        var repo = CreateRepository(deleteContext);
        await repo.DeleteAsync(config.Id);
        await deleteContext.SaveChangesAsync();

        // Assert
        await using var readContext = CreateContext();
        var retrieved = await readContext.McpServerConfigs.FindAsync(config.Id);
        retrieved.ShouldBeNull();

        var whitelists = await readContext.McpToolWhitelists
            .Where(w => w.McpServerConfigId == config.Id)
            .ToListAsync();
        whitelists.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_is_noop_when_not_found()
    {
        var repo = CreateRepository();
        await repo.DeleteAsync(Guid.NewGuid());
        await Context.SaveChangesAsync();
    }

    [Fact]
    public async Task SeedData_contains_four_bundled_configs_all_disabled()
    {
        // Arrange
        await using var readContext = CreateContext();

        // Act
        var bundled = await readContext.McpServerConfigs
            .AsNoTracking()
            .Where(c => c.IsBundled)
            .OrderBy(c => c.Name)
            .ToListAsync();

        // Assert
        bundled.Count.ShouldBe(4);
        bundled.ShouldAllBe(c => !c.IsEnabled);
        bundled.ShouldAllBe(c => c.IsBundled);
        bundled.ShouldAllBe(c => c.TransportType == McpTransportType.Stdio);
        bundled.ShouldAllBe(c => c.Command == "npx");

        bundled.ShouldContain(c => c.Name == "Brave Web Search");
        bundled.ShouldContain(c => c.Name == "Filesystem");
        bundled.ShouldContain(c => c.Name == "Database");
        bundled.ShouldContain(c => c.Name == "Code Execution");
    }

    [Fact]
    public async Task GetAllAsync_includes_tool_whitelists()
    {
        // Arrange
        var config = CreateConfig("With Whitelists");
        config.ToolWhitelists.Add(new McpToolWhitelist
        {
            Id = Guid.NewGuid(),
            McpServerConfigId = config.Id,
            ToolName = "tool-a",
            CreatedAt = DateTime.UtcNow,
        });
        config.ToolWhitelists.Add(new McpToolWhitelist
        {
            Id = Guid.NewGuid(),
            McpServerConfigId = config.Id,
            ToolName = "tool-b",
            CreatedAt = DateTime.UtcNow,
        });
        Context.McpServerConfigs.Add(config);
        await Context.SaveChangesAsync();

        await using var readContext = CreateContext();
        var repo = CreateRepository(readContext);

        // Act
        var results = await repo.GetAllAsync();

        // Assert
        var withWhitelists = results.First(c => c.Name == "With Whitelists");
        withWhitelists.ToolWhitelists.Count.ShouldBe(2);
    }
}
