namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Contracts.Versioning;
using nem.Mimir.Domain.Entities;

public class ArenaConfigConfiguration : IEntityTypeConfiguration<ArenaConfig>
{
    public void Configure(EntityTypeBuilder<ArenaConfig> builder)
    {
        builder.ToTable("arena_configs");

        builder.HasKey(ac => ac.Id);

        builder.Property(ac => ac.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(ac => ac.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(ac => ac.IsBlindComparisonEnabled)
            .HasColumnName("is_blind_comparison_enabled")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(ac => ac.ShowModelNamesAfterVote)
            .HasColumnName("show_model_names_after_vote")
            .HasDefaultValue(true)
            .IsRequired();

        var jsonOptions = NemJsonSerializerOptions.CreateOptions();

        builder.Property<List<string>>("_modelIds")
            .HasColumnName("model_ids")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<List<string>, string>(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>()),
                new ValueComparer<List<string>>(
                    (a, b) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions),
                    v => JsonSerializer.Serialize(v, jsonOptions).GetHashCode(),
                    v => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(v, jsonOptions), jsonOptions) ?? new List<string>()));

        builder.Property(ac => ac.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(ac => ac.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(ac => ac.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(ac => ac.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(ac => ac.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(ac => ac.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(ac => !ac.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(ac => ac.UserId)
            .IsUnique()
            .HasDatabaseName("ix_arena_configs_user_id");

        builder.Ignore(ac => ac.DomainEvents);
    }
}
