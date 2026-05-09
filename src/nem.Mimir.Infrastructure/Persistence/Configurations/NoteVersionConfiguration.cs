namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;

public class NoteVersionConfiguration : IEntityTypeConfiguration<NoteVersion>
{
    public void Configure(EntityTypeBuilder<NoteVersion> builder)
    {
        builder.ToTable("note_versions");

        builder.HasKey(version => version.Id);

        builder.Property(version => version.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(version => version.NoteId)
            .HasColumnName("note_id")
            .IsRequired();

        builder.Property(version => version.ContentSnapshot)
            .HasColumnName("content_snapshot")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(version => version.ChangeDescription)
            .HasColumnName("change_description")
            .HasMaxLength(500);

        builder.Property(version => version.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(version => version.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(version => version.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(version => version.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(version => version.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(version => version.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(version => version.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(version => !version.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(version => version.NoteId)
            .HasDatabaseName("ix_note_versions_note_id");

        builder.Ignore(version => version.DomainEvents);
    }
}
