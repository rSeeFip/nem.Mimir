namespace Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mimir.Domain.Entities;

public sealed class BackgroundTaskConfiguration : IEntityTypeConfiguration<BackgroundTask>
{
    public void Configure(EntityTypeBuilder<BackgroundTask> builder)
    {
        builder.ToTable("background_tasks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.AgentId)
            .HasColumnName("agent_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.SubmittedAt)
            .HasColumnName("submitted_at")
            .IsRequired();

        builder.Property(x => x.StartedAt)
            .HasColumnName("started_at");

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(x => x.Result)
            .HasColumnName("result")
            .HasMaxLength(2048);

        builder.Property(x => x.Error)
            .HasColumnName("error")
            .HasMaxLength(2000);

        builder.Property(x => x.Priority)
            .HasColumnName("priority")
            .IsRequired();

        builder.HasIndex(x => x.TaskId)
            .HasDatabaseName("ix_background_tasks_task_id")
            .IsUnique();

        builder.HasIndex(x => new { x.Status, x.SubmittedAt })
            .HasDatabaseName("ix_background_tasks_status_submitted_at");

        builder.Ignore(x => x.DomainEvents);
    }
}
