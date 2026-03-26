namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;

public class EvaluationFeedbackConfiguration : IEntityTypeConfiguration<EvaluationFeedback>
{
    public void Configure(EntityTypeBuilder<EvaluationFeedback> builder)
    {
        builder.ToTable("evaluation_feedback");

        builder.HasKey(feedback => feedback.Id);

        builder.Property(feedback => feedback.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(feedback => feedback.EvaluationId)
            .HasColumnName("evaluation_id")
            .IsRequired();

        builder.Property(feedback => feedback.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(feedback => feedback.Quality)
            .HasColumnName("quality")
            .IsRequired();

        builder.Property(feedback => feedback.Relevance)
            .HasColumnName("relevance")
            .IsRequired();

        builder.Property(feedback => feedback.Accuracy)
            .HasColumnName("accuracy")
            .IsRequired();

        builder.Property(feedback => feedback.Score)
            .HasColumnName("score")
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(feedback => feedback.Comment)
            .HasColumnName("comment")
            .HasMaxLength(2000);

        builder.Property(feedback => feedback.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(feedback => feedback.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(feedback => feedback.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(feedback => feedback.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(feedback => feedback.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(feedback => feedback.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(feedback => !feedback.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(feedback => feedback.EvaluationId)
            .HasDatabaseName("ix_evaluation_feedback_evaluation_id");

        builder.HasIndex(feedback => feedback.UserId)
            .HasDatabaseName("ix_evaluation_feedback_user_id");

        builder.Ignore(feedback => feedback.DomainEvents);
    }
}
