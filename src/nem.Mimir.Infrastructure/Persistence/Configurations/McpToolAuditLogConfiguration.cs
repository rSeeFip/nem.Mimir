namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.McpServers;

public class McpToolAuditLogConfiguration : IEntityTypeConfiguration<McpToolAuditLog>
{
    public void Configure(EntityTypeBuilder<McpToolAuditLog> builder)
    {
        builder.ToTable("mcp_tool_audit_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.McpServerConfigId)
            .HasColumnName("mcp_server_config_id");

        builder.Property(e => e.ToolName)
            .HasColumnName("tool_name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Input)
            .HasColumnName("input");

        builder.Property(e => e.Output)
            .HasColumnName("output");

        builder.Property(e => e.LatencyMs)
            .HasColumnName("latency_ms")
            .IsRequired();

        builder.Property(e => e.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(200);

        builder.Property(e => e.ConversationId)
            .HasColumnName("conversation_id");

        builder.Property(e => e.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName("ix_mcp_tool_audit_logs_timestamp");

        builder.HasIndex(e => e.McpServerConfigId)
            .HasDatabaseName("ix_mcp_tool_audit_logs_mcp_server_config_id");

        builder.HasIndex(e => e.ToolName)
            .HasDatabaseName("ix_mcp_tool_audit_logs_tool_name");

        builder.HasOne(e => e.McpServerConfig)
            .WithMany()
            .HasForeignKey(e => e.McpServerConfigId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
