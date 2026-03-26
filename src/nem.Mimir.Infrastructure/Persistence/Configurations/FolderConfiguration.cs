namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;

public class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.ToTable("folders");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(f => f.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(f => f.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.ParentId)
            .HasColumnName("parent_id");

        builder.Property(f => f.IsExpanded)
            .HasColumnName("is_expanded")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(f => f.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(f => f.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(f => f.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(f => f.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(f => f.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(f => !f.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(f => f.UserId)
            .HasDatabaseName("ix_folders_user_id");

        builder.HasIndex(f => new { f.UserId, f.ParentId })
            .HasDatabaseName("ix_folders_user_parent_id");

        builder.Ignore(f => f.DomainEvents);
    }
}
