namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;

public class LeaderboardEntryConfiguration : IEntityTypeConfiguration<LeaderboardEntry>
{
    public void Configure(EntityTypeBuilder<LeaderboardEntry> builder)
    {
        builder.ToTable("leaderboard_entries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(entry => entry.ModelId)
            .HasColumnName("model_id")
            .IsRequired();

        builder.Property(entry => entry.EloScore)
            .HasColumnName("elo_score")
            .HasPrecision(10, 4)
            .IsRequired();

        builder.Property(entry => entry.Wins)
            .HasColumnName("wins")
            .IsRequired();

        builder.Property(entry => entry.Losses)
            .HasColumnName("losses")
            .IsRequired();

        builder.Property(entry => entry.Draws)
            .HasColumnName("draws")
            .IsRequired();

        builder.Property(entry => entry.TotalEvaluations)
            .HasColumnName("total_evaluations")
            .IsRequired();

        builder.Property(entry => entry.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(entry => entry.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(entry => entry.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(entry => entry.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(entry => entry.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(entry => entry.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(entry => !entry.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(entry => entry.ModelId)
            .HasDatabaseName("ix_leaderboard_entries_model_id")
            .IsUnique();

        builder.HasIndex(entry => entry.EloScore)
            .HasDatabaseName("ix_leaderboard_entries_elo_score");

        builder.Ignore(entry => entry.DomainEvents);
    }
}
