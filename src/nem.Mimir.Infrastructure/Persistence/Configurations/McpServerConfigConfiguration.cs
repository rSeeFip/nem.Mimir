namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.McpServers;

public class McpServerConfigConfiguration : IEntityTypeConfiguration<McpServerConfig>
{
    public void Configure(EntityTypeBuilder<McpServerConfig> builder)
    {
        builder.ToTable("mcp_server_configs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(c => c.TransportType)
            .HasColumnName("transport_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Command)
            .HasColumnName("command")
            .HasMaxLength(500);

        builder.Property(c => c.Arguments)
            .HasColumnName("arguments")
            .HasMaxLength(2000);

        builder.Property(c => c.Url)
            .HasColumnName("url")
            .HasMaxLength(2000);

        builder.Property(c => c.EnvironmentVariablesJson)
            .HasColumnName("environment_variables_json")
            .HasColumnType("jsonb");

        builder.Property(c => c.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired();

        builder.Property(c => c.IsBundled)
            .HasColumnName("is_bundled")
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasDatabaseName("ix_mcp_server_configs_name");

        builder.HasIndex(c => c.IsEnabled)
            .HasDatabaseName("ix_mcp_server_configs_is_enabled");

        builder.HasMany(c => c.ToolWhitelists)
            .WithOne(w => w.McpServerConfig)
            .HasForeignKey(w => w.McpServerConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        SeedBundledConfigs(builder);
    }

    private static void SeedBundledConfigs(EntityTypeBuilder<McpServerConfig> builder)
    {
        var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new McpServerConfig
            {
                Id = new Guid("a1b2c3d4-0001-0000-0000-000000000001"),
                Name = "Brave Web Search",
                Description = "Web search powered by Brave Search API",
                TransportType = McpTransportType.Stdio,
                Command = "npx",
                Arguments = "-y @anthropic/brave-search-mcp",
                IsEnabled = false,
                IsBundled = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
            },
            new McpServerConfig
            {
                Id = new Guid("a1b2c3d4-0002-0000-0000-000000000002"),
                Name = "Filesystem",
                Description = "Local filesystem access via MCP",
                TransportType = McpTransportType.Stdio,
                Command = "npx",
                Arguments = "-y @anthropic/filesystem-mcp /tmp",
                IsEnabled = false,
                IsBundled = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
            },
            new McpServerConfig
            {
                Id = new Guid("a1b2c3d4-0003-0000-0000-000000000003"),
                Name = "Database",
                Description = "Database query and management via MCP",
                TransportType = McpTransportType.Stdio,
                Command = "npx",
                Arguments = "-y @anthropic/database-mcp",
                IsEnabled = false,
                IsBundled = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
            },
            new McpServerConfig
            {
                Id = new Guid("a1b2c3d4-0004-0000-0000-000000000004"),
                Name = "Code Execution",
                Description = "Sandboxed code execution via MCP",
                TransportType = McpTransportType.Stdio,
                Command = "npx",
                Arguments = "-y @anthropic/code-execution-mcp",
                IsEnabled = false,
                IsBundled = true,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
            });
    }
}
