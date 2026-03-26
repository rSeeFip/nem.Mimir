namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;

public class NoteCollaboratorConfiguration : IEntityTypeConfiguration<NoteCollaborator>
{
    public void Configure(EntityTypeBuilder<NoteCollaborator> builder)
    {
        builder.ToTable("note_collaborators");

        builder.HasKey(collaborator => collaborator.Id);

        builder.Property(collaborator => collaborator.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(collaborator => collaborator.NoteId)
            .HasColumnName("note_id")
            .IsRequired();

        builder.Property(collaborator => collaborator.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(collaborator => collaborator.Permission)
            .HasColumnName("permission")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(collaborator => collaborator.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(collaborator => collaborator.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(collaborator => collaborator.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(collaborator => collaborator.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(collaborator => collaborator.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(collaborator => collaborator.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(collaborator => !collaborator.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(collaborator => new { collaborator.NoteId, collaborator.UserId })
            .HasDatabaseName("ix_note_collaborators_note_id_user_id");

        builder.HasIndex(collaborator => collaborator.UserId)
            .HasDatabaseName("ix_note_collaborators_user_id");

        builder.Ignore(collaborator => collaborator.DomainEvents);
    }
}
