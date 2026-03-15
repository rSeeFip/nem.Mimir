namespace nem.Mimir.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using nem.Mimir.Domain.Entities;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(a => a.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(a => a.Action)
            .HasColumnName("action")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(200);

        builder.Property(a => a.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(a => a.Details)
            .HasColumnName("details")
            .HasColumnType("text");

        builder.Property(a => a.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.HasIndex(a => new { a.UserId, a.Timestamp })
            .HasDatabaseName("ix_audit_entries_user_id_timestamp");

        // Ignore domain events collection
        builder.Ignore(a => a.DomainEvents);
    }
}
