namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Mimir.Domain.Entities;
using nem.Contracts.Content;
using nem.Contracts.Versioning;

public class ChannelEventConfiguration : IEntityTypeConfiguration<ChannelEvent>
{
    public void Configure(EntityTypeBuilder<ChannelEvent> builder)
    {
        builder.ToTable("channel_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ExternalChannelId)
            .HasColumnName("external_channel_id")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.ExternalUserId)
            .HasColumnName("external_user_id")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.ReceivedAt)
            .HasColumnName("received_at")
            .IsRequired();

        var jsonOptions = NemJsonSerializerOptions.CreateOptions();

        builder.Property(e => e.ContentPayload)
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

        builder.HasIndex(e => new { e.Channel, e.ExternalChannelId })
            .HasDatabaseName("ix_channel_events_channel_external_channel_id");

        builder.Ignore(e => e.DomainEvents);
    }
}
