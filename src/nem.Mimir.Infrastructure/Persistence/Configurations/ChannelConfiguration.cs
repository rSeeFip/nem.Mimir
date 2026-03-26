namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.Enums;

public class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.ToTable("channels");

        builder.HasKey(channel => channel.Id);

        builder.Property(channel => channel.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(channel => channel.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(channel => channel.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(channel => channel.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        builder.Property(channel => channel.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(channel => channel.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(channel => channel.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(channel => channel.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(channel => channel.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(channel => channel.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(channel => channel.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(channel => !channel.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasMany(channel => channel.Members)
            .WithOne()
            .HasForeignKey(member => member.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(channel => channel.Messages)
            .WithOne()
            .HasForeignKey(message => message.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(channel => channel.OwnerId)
            .HasDatabaseName("ix_channels_owner_id");

        builder.HasIndex(channel => new { channel.Type, channel.Name })
            .HasDatabaseName("ix_channels_type_name");

        builder.Ignore(channel => channel.DomainEvents);
    }
}
