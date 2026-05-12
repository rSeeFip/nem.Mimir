namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;

public class EvaluationConfiguration : IEntityTypeConfiguration<Evaluation>
{
    public void Configure(EntityTypeBuilder<Evaluation> builder)
    {
        builder.ToTable("evaluations");

        builder.HasKey(evaluation => evaluation.Id);

        builder.Property(evaluation => evaluation.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(evaluation => evaluation.ModelAId)
            .HasColumnName("model_a_id")
            .IsRequired();

        builder.Property(evaluation => evaluation.ModelBId)
            .HasColumnName("model_b_id")
            .IsRequired();

        builder.Property(evaluation => evaluation.Prompt)
            .HasColumnName("prompt")
            .HasMaxLength(8000)
            .IsRequired();

        builder.Property(evaluation => evaluation.ResponseA)
            .HasColumnName("response_a")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(evaluation => evaluation.ResponseB)
            .HasColumnName("response_b")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(evaluation => evaluation.Winner)
            .HasColumnName("winner")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(evaluation => evaluation.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(evaluation => evaluation.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(evaluation => evaluation.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(evaluation => evaluation.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(evaluation => evaluation.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(evaluation => evaluation.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(evaluation => evaluation.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(evaluation => evaluation.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(evaluation => !evaluation.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(evaluation => evaluation.UserId)
            .HasDatabaseName("ix_evaluations_user_id");

        builder.HasIndex(evaluation => new { evaluation.ModelAId, evaluation.ModelBId })
            .HasDatabaseName("ix_evaluations_models");

        builder.Ignore(evaluation => evaluation.DomainEvents);
    }
}
