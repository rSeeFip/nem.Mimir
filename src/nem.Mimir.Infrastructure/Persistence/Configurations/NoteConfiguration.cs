namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Contracts.Versioning;
using nem.Mimir.Domain.Entities;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes");

        builder.HasKey(note => note.Id);

        builder.Property(note => note.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(note => note.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        builder.Property(note => note.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(note => note.Content)
            .HasColumnName("content")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(note => note.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(note => note.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(note => note.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(note => note.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(note => note.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(note => note.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(note => !note.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        var jsonOptions = NemJsonSerializerOptions.CreateOptions();

        builder.Property<List<string>>("_tags")
            .HasColumnName("tags")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<List<string>, string>(
                    value => JsonSerializer.Serialize(value, jsonOptions),
                    value => JsonSerializer.Deserialize<List<string>>(value, jsonOptions) ?? new List<string>()),
                new ValueComparer<List<string>>(
                    (left, right) => JsonSerializer.Serialize(left, jsonOptions) == JsonSerializer.Serialize(right, jsonOptions),
                    value => JsonSerializer.Serialize(value, jsonOptions).GetHashCode(),
                    value => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(value, jsonOptions), jsonOptions) ?? new List<string>()));

        builder.HasMany(note => note.Collaborators)
            .WithOne()
            .HasForeignKey(collaborator => collaborator.NoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(note => note.Versions)
            .WithOne()
            .HasForeignKey(version => version.NoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(note => note.OwnerId)
            .HasDatabaseName("ix_notes_owner_id");

        builder.HasIndex(note => note.Title)
            .HasDatabaseName("ix_notes_title");

        builder.Ignore(note => note.DomainEvents);
    }
}
