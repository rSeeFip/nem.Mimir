namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Contracts.Versioning;
using nem.Mimir.Domain.Entities;

public class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplate>
{
    public void Configure(EntityTypeBuilder<PromptTemplate> builder)
    {
        builder.ToTable("prompt_templates");

        builder.HasKey(pt => pt.Id);

        builder.Property(pt => pt.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(pt => pt.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(pt => pt.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(pt => pt.Command)
            .HasColumnName("command")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(pt => pt.Content)
            .HasColumnName("content")
            .HasMaxLength(20000)
            .IsRequired();

        builder.Property(pt => pt.IsShared)
            .HasColumnName("is_shared")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(pt => pt.UsageCount)
            .HasColumnName("usage_count")
            .HasDefaultValue(0)
            .IsRequired();

        var jsonOptions = NemJsonSerializerOptions.CreateOptions();

        builder.Property<List<string>>("_tags")
            .HasColumnName("tags")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<List<string>, string>(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>()),
                new ValueComparer<List<string>>(
                    (a, b) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions),
                    v => JsonSerializer.Serialize(v, jsonOptions).GetHashCode(),
                    v => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(v, jsonOptions), jsonOptions) ?? new List<string>()));

        builder.Property<List<PromptTemplateVersionEntry>>("_versionHistory")
            .HasColumnName("version_history")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<List<PromptTemplateVersionEntry>, string>(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<List<PromptTemplateVersionEntry>>(v, jsonOptions) ?? new List<PromptTemplateVersionEntry>()),
                new ValueComparer<List<PromptTemplateVersionEntry>>(
                    (a, b) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions),
                    v => JsonSerializer.Serialize(v, jsonOptions).GetHashCode(),
                    v => JsonSerializer.Deserialize<List<PromptTemplateVersionEntry>>(JsonSerializer.Serialize(v, jsonOptions), jsonOptions) ?? new List<PromptTemplateVersionEntry>()));

        builder.Property(pt => pt.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(pt => pt.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(pt => pt.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(pt => pt.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(pt => pt.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(pt => pt.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(pt => !pt.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(pt => new { pt.UserId, pt.Command })
            .IsUnique()
            .HasDatabaseName("ix_prompt_templates_user_id_command");

        builder.HasIndex(pt => pt.Command)
            .HasDatabaseName("ix_prompt_templates_command");

        builder.HasIndex(pt => pt.IsShared)
            .HasDatabaseName("ix_prompt_templates_is_shared");

        builder.Ignore(pt => pt.DomainEvents);
    }
}
