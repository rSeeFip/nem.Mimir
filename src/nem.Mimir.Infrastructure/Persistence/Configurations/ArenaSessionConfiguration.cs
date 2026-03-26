namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;

public class ArenaSessionConfiguration : IEntityTypeConfiguration<ArenaSession>
{
    public void Configure(EntityTypeBuilder<ArenaSession> builder)
    {
        builder.ToTable("arena_sessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(session => session.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(session => session.Prompt)
            .HasColumnName("prompt")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(session => session.ModelAId)
            .HasColumnName("model_a_id")
            .IsRequired();

        builder.Property(session => session.ModelBId)
            .HasColumnName("model_b_id")
            .IsRequired();

        builder.Property(session => session.ResponseA)
            .HasColumnName("response_a")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(session => session.ResponseB)
            .HasColumnName("response_b")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(session => session.VotedWinner)
            .HasColumnName("voted_winner")
            .HasMaxLength(16);

        builder.Property(session => session.IsRevealed)
            .HasColumnName("is_revealed")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(session => session.IsCompleted)
            .HasColumnName("is_completed")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(session => session.ModelAName)
            .HasColumnName("model_a_name")
            .HasMaxLength(255);

        builder.Property(session => session.ModelBName)
            .HasColumnName("model_b_name")
            .HasMaxLength(255);

        builder.Property(session => session.ModelAEloBefore)
            .HasColumnName("model_a_elo_before")
            .HasPrecision(10, 4);

        builder.Property(session => session.ModelAEloAfter)
            .HasColumnName("model_a_elo_after")
            .HasPrecision(10, 4);

        builder.Property(session => session.ModelBEloBefore)
            .HasColumnName("model_b_elo_before")
            .HasPrecision(10, 4);

        builder.Property(session => session.ModelBEloAfter)
            .HasColumnName("model_b_elo_after")
            .HasPrecision(10, 4);

        builder.Property(session => session.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(session => session.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(session => session.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(session => session.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(session => session.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(session => session.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(session => !session.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(session => session.UserId)
            .HasDatabaseName("ix_arena_sessions_user_id");

        builder.HasIndex(session => session.IsCompleted)
            .HasDatabaseName("ix_arena_sessions_is_completed");

        builder.Ignore(session => session.DomainEvents);
    }
}
