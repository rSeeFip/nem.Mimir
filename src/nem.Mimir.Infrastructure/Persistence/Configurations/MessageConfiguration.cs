namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;
using nem.Contracts.Content;
using nem.Contracts.Versioning;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(m => m.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(m => m.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Content)
            .HasColumnName("content")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(m => m.Model)
            .HasColumnName("model")
            .HasMaxLength(200);

        builder.Property(m => m.TokenCount)
            .HasColumnName("token_count");

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        var jsonOptions = NemJsonSerializerOptions.CreateOptions();

        builder.Property(m => m.ContentPayload)
            .HasColumnName("content_payload")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<IContentPayload?, string?>(
                    v => v == null ? null : JsonSerializer.Serialize(v, jsonOptions),
                    v => v == null ? null : JsonSerializer.Deserialize<IContentPayload>(v, jsonOptions)),
                new ValueComparer<IContentPayload?>(
                    (a, b) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions),
                    v => v == null ? 0 : JsonSerializer.Serialize(v, jsonOptions).GetHashCode(),
                    v => v));

        builder.Property(m => m.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(50);

        builder.HasIndex(m => m.ConversationId)
            .HasDatabaseName("ix_messages_conversation_id");

        builder.HasIndex(m => m.ContentType)
            .HasDatabaseName("ix_messages_content_type");

        builder.Ignore(m => m.DomainEvents);
        builder.Ignore(m => m.ResolvedPayload);
    }
}
