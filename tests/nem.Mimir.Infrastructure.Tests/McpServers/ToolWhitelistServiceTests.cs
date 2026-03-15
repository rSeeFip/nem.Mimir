namespace nem.Mimir.Infrastructure.Tests.McpServers;

using nem.Mimir.Domain.McpServers;
using nem.Mimir.Infrastructure.McpServers;
using nem.Mimir.Infrastructure.Persistence;
using nem.Mimir.Infrastructure.Tests.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class ToolWhitelistServiceTests : RepositoryTestBase
{
    private ToolWhitelistService CreateService() => new(Context);

    private ToolWhitelistService CreateService(MimirDbContext context) => new(context);

    private async Task<McpServerConfig> SeedServerConfigAsync()
    {
        var config = new McpServerConfig
        {
            Id = Guid.NewGuid(),
            Name = "Test Server",
            TransportType = McpTransportType.Stdio,
            Command = "npx",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await Context.McpServerConfigs.AddAsync(config);
        await Context.SaveChangesAsync();
        return config;
    }

    private async Task<McpToolWhitelist> SeedWhitelistEntryAsync(
        Guid mcpServerConfigId,
        string toolName,
        bool isEnabled = true)
    {
        var entry = new McpToolWhitelist
        {
            Id = Guid.NewGuid(),
            McpServerConfigId = mcpServerConfigId,
            ToolName = toolName,
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow,
        };
        await Context.McpToolWhitelists.AddAsync(entry);
        await Context.SaveChangesAsync();
        return entry;
    }

    [Fact]
    public async Task IsToolAllowedAsync_returns_true_for_enabled_tool()
    {
        var config = await SeedServerConfigAsync();
        await SeedWhitelistEntryAsync(config.Id, "read_file", isEnabled: true);

        await using var readContext = CreateContext();
        var service = CreateService(readContext);

        var result = await service.IsToolAllowedAsync(config.Id, "read_file");

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsToolAllowedAsync_returns_false_for_disabled_tool()
    {
        var config = await SeedServerConfigAsync();
        await SeedWhitelistEntryAsync(config.Id, "delete_file", isEnabled: false);

        await using var readContext = CreateContext();
        var service = CreateService(readContext);

        var result = await service.IsToolAllowedAsync(config.Id, "delete_file");

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsToolAllowedAsync_returns_false_for_missing_tool()
    {
        var config = await SeedServerConfigAsync();

        var service = CreateService();

        var result = await service.IsToolAllowedAsync(config.Id, "nonexistent_tool");

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllowedToolsAsync_returns_only_enabled_tools()
    {
        var config = await SeedServerConfigAsync();
        await SeedWhitelistEntryAsync(config.Id, "tool_a", isEnabled: true);
        await SeedWhitelistEntryAsync(config.Id, "tool_b", isEnabled: false);
        await SeedWhitelistEntryAsync(config.Id, "tool_c", isEnabled: true);

        await using var readContext = CreateContext();
        var service = CreateService(readContext);

        var allowed = await service.GetAllowedToolsAsync(config.Id);

        allowed.Count.ShouldBe(2);
        allowed.ShouldAllBe(w => w.IsEnabled);
        allowed.Select(w => w.ToolName).ShouldBe(new[] { "tool_a", "tool_c" });
    }

    [Fact]
    public async Task SetToolWhitelistAsync_creates_new_entry()
    {
        var config = await SeedServerConfigAsync();

        var service = CreateService();

        await service.SetToolWhitelistAsync(config.Id, "new_tool", true);

        await using var readContext = CreateContext();
        var entry = await readContext.McpToolWhitelists
            .FirstOrDefaultAsync(w => w.McpServerConfigId == config.Id && w.ToolName == "new_tool");
        entry.ShouldNotBeNull();
        entry.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task SetToolWhitelistAsync_updates_existing_entry()
    {
        var config = await SeedServerConfigAsync();
        await SeedWhitelistEntryAsync(config.Id, "toggle_tool", isEnabled: true);

        await using var updateContext = CreateContext();
        var service = CreateService(updateContext);

        await service.SetToolWhitelistAsync(config.Id, "toggle_tool", false);

        await using var readContext = CreateContext();
        var entry = await readContext.McpToolWhitelists
            .FirstOrDefaultAsync(w => w.McpServerConfigId == config.Id && w.ToolName == "toggle_tool");
        entry.ShouldNotBeNull();
        entry.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void ValidateArguments_accepts_valid_json()
    {
        var service = CreateService();

        service.ValidateArguments("""{"path": "/home/user/file.txt", "content": "hello"}""")
            .ShouldBeTrue();
    }

    [Fact]
    public void ValidateArguments_accepts_empty_arguments()
    {
        var service = CreateService();

        service.ValidateArguments(string.Empty).ShouldBeTrue();
    }

    [Fact]
    public void ValidateArguments_rejects_oversized_arguments()
    {
        var service = CreateService();

        var oversized = new string('x', 10_241);

        service.ValidateArguments(oversized).ShouldBeFalse();
    }

    [Theory]
    [InlineData("""{"path": "../../../etc/passwd"}""")]
    [InlineData("""{"path": "..\\windows\\system32"}""")]
    [InlineData("""{"path": "/etc/shadow"}""")]
    [InlineData("""{"path": "/proc/self/environ"}""")]
    [InlineData("""{"path": "C:\\Windows\\System32"}""")]
    public void ValidateArguments_rejects_path_traversal(string argumentsJson)
    {
        var service = CreateService();

        service.ValidateArguments(argumentsJson).ShouldBeFalse();
    }

    [Theory]
    [InlineData("""{"query": "'; DROP TABLE users"}""")]
    [InlineData("""{"query": "x UNION SELECT * FROM passwords"}""")]
    [InlineData("""{"query": "admin' OR 1=1"}""")]
    [InlineData("""{"query": "value -- comment"}""")]
    public void ValidateArguments_rejects_sql_injection(string argumentsJson)
    {
        var service = CreateService();

        service.ValidateArguments(argumentsJson).ShouldBeFalse();
    }
}
