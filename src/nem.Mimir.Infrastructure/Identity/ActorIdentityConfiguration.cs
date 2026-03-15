namespace nem.Mimir.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ActorIdentityConfiguration
    : IEntityTypeConfiguration<ActorIdentityDocument>,
      IEntityTypeConfiguration<ChannelIdentityLinkDocument>
{
    public void Configure(EntityTypeBuilder<ActorIdentityDocument> builder)
    {
        builder.ToTable("actor_identities");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(a => a.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(a => a.Email)
            .HasColumnName("email")
            .HasMaxLength(256);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasMany(a => a.Links)
            .WithOne(l => l.ActorIdentity)
            .HasForeignKey(l => l.ActorIdentityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin")
            .IsRowVersion();
    }

    public void Configure(EntityTypeBuilder<ChannelIdentityLinkDocument> builder)
    {
        builder.ToTable("channel_identity_links");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(l => l.ActorIdentityId)
            .HasColumnName("actor_identity_id")
            .IsRequired();

        builder.Property(l => l.ChannelType)
            .HasColumnName("channel_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.ProviderUserId)
            .HasColumnName("provider_user_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(l => l.TrustLevel)
            .HasColumnName("trust_level")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.LinkedAt)
            .HasColumnName("linked_at")
            .IsRequired();

        builder.Property(l => l.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(l => new { l.ChannelType, l.ProviderUserId })
            .HasDatabaseName("ix_channel_identity_links_channel_provider");

        builder.HasIndex(l => l.ProviderUserId)
            .HasDatabaseName("ix_channel_identity_links_provider_user_id");

        builder.Property<uint>("xmin")
            .IsRowVersion();
    }
}
