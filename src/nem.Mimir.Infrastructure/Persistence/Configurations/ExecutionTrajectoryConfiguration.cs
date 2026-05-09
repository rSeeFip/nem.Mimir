namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Contracts.Versioning;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

public sealed class ExecutionTrajectoryConfiguration : IEntityTypeConfiguration<ExecutionTrajectory>
{
    public void Configure(EntityTypeBuilder<ExecutionTrajectory> builder)
    {
        builder.ToTable("execution_trajectories");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.SessionId)
            .HasColumnName("session_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.AgentId)
            .HasColumnName("agent_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(e => e.IsSuccess)
            .HasColumnName("is_success")
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.Property(e => e.TotalSteps)
            .HasColumnName("total_steps")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");

        var jsonOptions = NemJsonSerializerOptions.CreateOptions();

        builder.Property<List<TrajectoryStep>>("_steps")
            .HasColumnName("steps")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<List<TrajectoryStep>, string>(
                    value => JsonSerializer.Serialize(value, jsonOptions),
                    value => JsonSerializer.Deserialize<List<TrajectoryStep>>(value, jsonOptions) ?? new List<TrajectoryStep>()),
                new ValueComparer<List<TrajectoryStep>>(
                    (left, right) => JsonSerializer.Serialize(left, jsonOptions) == JsonSerializer.Serialize(right, jsonOptions),
                    value => JsonSerializer.Serialize(value, jsonOptions).GetHashCode(),
                    value => JsonSerializer.Deserialize<List<TrajectoryStep>>(JsonSerializer.Serialize(value, jsonOptions), jsonOptions) ?? new List<TrajectoryStep>()));

        builder.HasIndex(e => e.SessionId)
            .HasDatabaseName("ix_execution_trajectories_session_id");

        builder.HasIndex(e => e.AgentId)
            .HasDatabaseName("ix_execution_trajectories_agent_id");

        builder.HasIndex(e => e.StartedAt)
            .HasDatabaseName("ix_execution_trajectories_started_at");

        builder.Ignore(e => e.Steps);
        builder.Ignore(e => e.DomainEvents);
    }
}
