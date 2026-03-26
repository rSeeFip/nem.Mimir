namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;

public class ImageGenerationConfiguration : IEntityTypeConfiguration<ImageGeneration>
{
    public void Configure(EntityTypeBuilder<ImageGeneration> builder)
    {
        builder.ToTable("image_generations");

        builder.HasKey(image => image.Id);

        builder.Property(image => image.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(image => image.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(image => image.Prompt)
            .HasColumnName("prompt")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(image => image.NegativePrompt)
            .HasColumnName("negative_prompt")
            .HasMaxLength(4000);

        builder.Property(image => image.Model)
            .HasColumnName("model")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(image => image.Size)
            .HasColumnName("size")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(image => image.Quality)
            .HasColumnName("quality")
            .HasMaxLength(32);

        builder.Property(image => image.NumberOfImages)
            .HasColumnName("number_of_images")
            .IsRequired();

        builder.Property(image => image.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(image => image.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(2000);

        builder.Property(image => image.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.Property(image => image.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(image => image.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(image => image.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(image => image.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(image => image.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(image => image.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(image => image.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(image => !image.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(image => image.UserId)
            .HasDatabaseName("ix_image_generations_user_id");

        builder.HasIndex(image => image.Status)
            .HasDatabaseName("ix_image_generations_status");

        builder.Ignore(image => image.DomainEvents);
    }
}
