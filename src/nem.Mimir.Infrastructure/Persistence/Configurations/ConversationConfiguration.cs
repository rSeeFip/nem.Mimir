namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Contracts.Versioning;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Mimir.Domain.ValueObjects;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(c => c.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(c => c.Title)
            .HasColumnName("title")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.FolderId)
            .HasColumnName("folder_id");

        builder.Property(c => c.IsPinned)
            .HasColumnName("is_pinned")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.ShareId)
            .HasColumnName("share_id")
            .HasMaxLength(50);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(c => c.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(c => c.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(c => !c.IsDeleted);

        // PostgreSQL optimistic concurrency via xmin
        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(c => c.UserId)
            .HasDatabaseName("ix_conversations_user_id");

        builder.HasIndex(c => c.ShareId)
            .IsUnique()
            .HasDatabaseName("ix_conversations_share_id")
            .HasFilter("share_id IS NOT NULL");

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

        builder.OwnsOne(c => c.Settings, s =>
        {
            s.Property(p => p.MaxTokens)
                .HasColumnName("settings_max_tokens")
                .HasDefaultValue(4096);

            s.Property(p => p.Temperature)
                .HasColumnName("settings_temperature")
                .HasDefaultValue(0.7);

            s.Property(p => p.Model)
                .HasColumnName("settings_model")
                .HasMaxLength(100);

            s.Property(p => p.AutoArchiveAfterDays)
                .HasColumnName("settings_auto_archive_days")
                .HasDefaultValue(30);

            s.Property(p => p.SystemPromptId)
                .HasConversion<Guid?>(
                    v => v.HasValue ? v.Value.Value : (Guid?)null,
                    v => v.HasValue ? new SystemPromptId(v.Value) : null)
                .HasColumnName("settings_system_prompt_id");

        });

        // Navigation: Messages owned by Conversation

        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore domain events collection
        builder.Ignore(c => c.DomainEvents);
    }
}
