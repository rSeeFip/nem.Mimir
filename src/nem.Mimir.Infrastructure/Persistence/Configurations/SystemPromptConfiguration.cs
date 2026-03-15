namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;

public class SystemPromptConfiguration : IEntityTypeConfiguration<SystemPrompt>
{
    public void Configure(EntityTypeBuilder<SystemPrompt> builder)
    {
        builder.ToTable("system_prompts");

        builder.HasKey(sp => sp.Id);

        builder.Property(sp => sp.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(sp => sp.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(sp => sp.Template)
            .HasColumnName("template")
            .HasMaxLength(10000)
            .IsRequired();

        builder.Property(sp => sp.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(sp => sp.IsDefault)
            .HasColumnName("is_default")
            .IsRequired();

        builder.Property(sp => sp.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(sp => sp.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(sp => sp.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(sp => sp.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(sp => sp.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(sp => sp.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(sp => sp.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(sp => !sp.IsDeleted);

        // PostgreSQL optimistic concurrency via xmin
        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(sp => sp.IsDefault)
            .HasDatabaseName("ix_system_prompts_is_default");

        builder.HasIndex(sp => sp.IsActive)
            .HasDatabaseName("ix_system_prompts_is_active");

        // Ignore domain events collection
        builder.Ignore(sp => sp.DomainEvents);
    }
}
