namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Contracts.Versioning;
using nem.Mimir.Domain.Entities;

public class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable("user_preferences");

        builder.HasKey(up => up.Id);

        builder.Property(up => up.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(up => up.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        var jsonOptions = NemJsonSerializerOptions.CreateOptions();

        builder.Property(up => up.Settings)
            .HasColumnName("settings")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<Dictionary<string, Dictionary<string, object>>, string>(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(v, jsonOptions)
                        ?? new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase)),
                new ValueComparer<Dictionary<string, Dictionary<string, object>>>(
                    (a, b) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions),
                    v => JsonSerializer.Serialize(v, jsonOptions).GetHashCode(),
                    v => JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(
                            JsonSerializer.Serialize(v, jsonOptions),
                            jsonOptions)
                        ?? new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase)));

        builder.Property(up => up.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(up => up.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(up => up.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(up => up.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(up => up.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(up => up.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(up => !up.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(up => up.UserId)
            .IsUnique()
            .HasDatabaseName("ix_user_preferences_user_id");

        builder.Ignore(up => up.DomainEvents);
    }
}
