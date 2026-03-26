namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using nem.Contracts.Versioning;
using nem.Mimir.Domain.Entities;

public class ModelProfileConfiguration : IEntityTypeConfiguration<ModelProfile>
{
    public void Configure(EntityTypeBuilder<ModelProfile> builder)
    {
        builder.ToTable("model_profiles");

        builder.HasKey(mp => mp.Id);

        builder.Property(mp => mp.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(mp => mp.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(mp => mp.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(mp => mp.ModelId)
            .HasColumnName("model_id")
            .HasMaxLength(200)
            .IsRequired();

        var jsonOptions = NemJsonSerializerOptions.CreateOptions();

        builder.Property(mp => mp.Parameters)
            .HasColumnName("parameters")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<ModelParameters, string>(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<ModelParameters>(v, jsonOptions) ?? ModelParameters.Default),
                new ValueComparer<ModelParameters>(
                    (a, b) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions),
                    v => JsonSerializer.Serialize(v, jsonOptions).GetHashCode(),
                    v => JsonSerializer.Deserialize<ModelParameters>(JsonSerializer.Serialize(v, jsonOptions), jsonOptions) ?? ModelParameters.Default));

        builder.Property(mp => mp.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(mp => mp.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(mp => mp.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(mp => mp.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(mp => mp.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(mp => mp.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(mp => !mp.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(mp => new { mp.UserId, mp.Name })
            .IsUnique()
            .HasDatabaseName("ix_model_profiles_user_id_name");

        builder.HasIndex(mp => mp.ModelId)
            .HasDatabaseName("ix_model_profiles_model_id");

        builder.Ignore(mp => mp.DomainEvents);
    }
}
