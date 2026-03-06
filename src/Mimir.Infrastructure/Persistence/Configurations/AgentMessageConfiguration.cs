namespace Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mimir.Domain.Entities;

public sealed class AgentMessageConfiguration : IEntityTypeConfiguration<AgentMessage>
{
    public void Configure(EntityTypeBuilder<AgentMessage> builder)
    {
        builder.ToTable("agent_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.MessageType)
            .HasColumnName("message_type")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.SourceAgentId)
            .HasColumnName("source_agent_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.TargetAgentId)
            .HasColumnName("target_agent_id")
            .HasMaxLength(200);

        builder.Property(x => x.Topic)
            .HasColumnName("topic")
            .HasMaxLength(200);

        builder.Property(x => x.Priority)
            .HasColumnName("priority")
            .IsRequired();

        builder.Property(x => x.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnName("payload")
            .HasColumnType("text")
            .IsRequired();

        builder.HasIndex(x => x.Timestamp)
            .HasDatabaseName("ix_agent_messages_timestamp");

        builder.HasIndex(x => new { x.SourceAgentId, x.TargetAgentId })
            .HasDatabaseName("ix_agent_messages_source_target");

        builder.HasIndex(x => x.Topic)
            .HasDatabaseName("ix_agent_messages_topic");

        builder.Ignore(x => x.DomainEvents);
    }
}
