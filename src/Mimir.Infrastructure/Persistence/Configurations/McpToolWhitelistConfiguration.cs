namespace Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mimir.Domain.McpServers;

public class McpToolWhitelistConfiguration : IEntityTypeConfiguration<McpToolWhitelist>
{
    public void Configure(EntityTypeBuilder<McpToolWhitelist> builder)
    {
        builder.ToTable("mcp_tool_whitelists");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(w => w.McpServerConfigId)
            .HasColumnName("mcp_server_config_id")
            .IsRequired();

        builder.Property(w => w.ToolName)
            .HasColumnName("tool_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(w => w.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired();

        builder.Property(w => w.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(w => new { w.McpServerConfigId, w.ToolName })
            .IsUnique()
            .HasDatabaseName("ix_mcp_tool_whitelists_config_id_tool_name");
    }
}
