namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Contracts.Versioning;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;

public class ChannelMessageConfiguration : IEntityTypeConfiguration<ChannelMessage>
{
    public void Configure(EntityTypeBuilder<ChannelMessage> builder)
    {
        builder.ToTable("channel_messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(message => message.ChannelId)
            .HasColumnName("channel_id")
            .IsRequired();

        builder.Property(message => message.SenderId)
            .HasColumnName("sender_id")
            .IsRequired();

        builder.Property(message => message.Content)
            .HasColumnName("content")
            .HasColumnType("text")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(message => message.ParentMessageId)
            .HasColumnName("parent_message_id");

        builder.Property(message => message.IsPinned)
            .HasColumnName("is_pinned")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(message => message.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(message => message.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(message => message.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(message => message.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(message => message.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(message => message.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(message => !message.IsDeleted);

        var jsonOptions = NemJsonSerializerOptions.CreateOptions();

        builder.Property<List<ChannelMessageReaction>>("_reactions")
            .HasColumnName("reactions")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<List<ChannelMessageReaction>, string>(
                    value => JsonSerializer.Serialize(value, jsonOptions),
                    value => JsonSerializer.Deserialize<List<ChannelMessageReaction>>(value, jsonOptions) ?? new List<ChannelMessageReaction>()),
                new ValueComparer<List<ChannelMessageReaction>>(
                    (left, right) => JsonSerializer.Serialize(left, jsonOptions) == JsonSerializer.Serialize(right, jsonOptions),
                    value => JsonSerializer.Serialize(value, jsonOptions).GetHashCode(),
                    value => JsonSerializer.Deserialize<List<ChannelMessageReaction>>(JsonSerializer.Serialize(value, jsonOptions), jsonOptions) ?? new List<ChannelMessageReaction>()));

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(message => message.ChannelId)
            .HasDatabaseName("ix_channel_messages_channel_id");

        builder.HasIndex(message => message.ParentMessageId)
            .HasDatabaseName("ix_channel_messages_parent_message_id");

        builder.Ignore(message => message.DomainEvents);
    }
}
