namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;

public class UserMemoryConfiguration : IEntityTypeConfiguration<UserMemory>
{
    public void Configure(EntityTypeBuilder<UserMemory> builder)
    {
        builder.ToTable("user_memories");

        builder.HasKey(um => um.Id);

        builder.Property(um => um.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(um => um.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(um => um.Content)
            .HasColumnName("content")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(um => um.Context)
            .HasColumnName("context")
            .HasMaxLength(1000);

        builder.Property(um => um.Source)
            .HasColumnName("source")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(um => um.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(um => um.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(um => um.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(um => um.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(um => um.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(um => um.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(um => um.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(um => !um.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(um => um.UserId)
            .HasDatabaseName("ix_user_memories_user_id");

        builder.HasIndex(um => new { um.UserId, um.IsActive })
            .HasDatabaseName("ix_user_memories_user_id_is_active");

        builder.Ignore(um => um.DomainEvents);
    }
}
