using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Config;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(log => log.Id);
        builder.ToTable("audit_logs");

        builder.Property(log => log.OccurredAt).HasDefaultValueSql("now()");
        builder.Property(log => log.ActorUsername).IsRequired().HasMaxLength(100);
        builder.Property(log => log.ActorFullName).HasMaxLength(150);
        builder.Property(log => log.ActorRole).HasMaxLength(50);
        builder.Property(log => log.Action).IsRequired().HasMaxLength(80);
        builder.Property(log => log.Category).IsRequired().HasMaxLength(30);
        builder.Property(log => log.EntityType).HasMaxLength(30);
        builder.Property(log => log.EntityLabel).HasMaxLength(250);
        builder.Property(log => log.Description).IsRequired().HasMaxLength(250);
        builder.Property(log => log.Outcome).IsRequired().HasMaxLength(10);
        builder.Property(log => log.HttpMethod).IsRequired().HasMaxLength(10);
        builder.Property(log => log.Path).IsRequired().HasMaxLength(200);
        builder.Property(log => log.IpAddress).HasMaxLength(50);

        builder.Property(log => log.Details)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.HasIndex(log => log.OccurredAt);
        builder.HasIndex(log => new { log.ActorUserId, log.OccurredAt }).IsDescending(false, true);
        builder.HasIndex(log => log.Action);
        builder.HasIndex(log => log.Category);
        builder.HasIndex(log => new { log.EntityType, log.EntityId });
    }
}
