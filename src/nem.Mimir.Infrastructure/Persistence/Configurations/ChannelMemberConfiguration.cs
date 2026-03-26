namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;

public class ChannelMemberConfiguration : IEntityTypeConfiguration<ChannelMember>
{
    public void Configure(EntityTypeBuilder<ChannelMember> builder)
    {
        builder.ToTable("channel_members");

        builder.HasKey(member => member.Id);

        builder.Property(member => member.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(member => member.ChannelId)
            .HasColumnName("channel_id")
            .IsRequired();

        builder.Property(member => member.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(member => member.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(member => member.JoinedAt)
            .HasColumnName("joined_at")
            .IsRequired();

        builder.Property(member => member.LeftAt)
            .HasColumnName("left_at");

        builder.Property(member => member.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(member => member.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(256);

        builder.Property(member => member.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(member => member.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(256);

        builder.Property(member => member.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(member => member.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(member => !member.IsDeleted);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.HasIndex(member => new { member.ChannelId, member.UserId })
            .HasDatabaseName("ix_channel_members_channel_id_user_id");

        builder.HasIndex(member => member.UserId)
            .HasDatabaseName("ix_channel_members_user_id");

        builder.Ignore(member => member.DomainEvents);
    }
}
