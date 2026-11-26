using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Config;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(notification => notification.Id);
        builder.ToTable("notifications");

        builder.Property(notification => notification.Type).IsRequired().HasMaxLength(50);
        builder.Property(notification => notification.ActorName).IsRequired().HasMaxLength(150);
        builder.Property(notification => notification.EntityType).IsRequired().HasMaxLength(30);
        builder.Property(notification => notification.EntityLabel).IsRequired().HasMaxLength(250);
        builder.Property(notification => notification.CreatedAt).HasDefaultValueSql("now()");

        builder.Property(notification => notification.Meta)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(notification => notification.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(notification => notification.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(notification => new { notification.RecipientUserId, notification.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(notification => notification.RecipientUserId)
            .HasDatabaseName("ix_notifications_recipient_user_id_unread")
            .HasFilter("read_at IS NULL");
    }
}
