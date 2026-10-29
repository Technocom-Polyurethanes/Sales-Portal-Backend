using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Config;

public class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Reference)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(o => new { o.Reference, o.Version })
            .IsUnique();

        // Use PostgreSQL's system column as the concurrency token.
        builder.Property<uint>("xmin")
            .IsRowVersion();

        // Map SnapshotJson column as PostgreSQL jsonb type
        builder.Property(o => o.SnapshotJson)
            .HasColumnType("jsonb")
            .IsRequired();

        // High precision decimal formatting for currency and tax fields
        builder.Property(o => o.Subtotal).HasColumnType("decimal(18,2)");
        builder.Property(o => o.OrderDiscount).HasColumnType("decimal(18,2)");
        builder.Property(o => o.OrderDiscountPercent).HasColumnType("decimal(5,2)");
        builder.Property(o => o.TaxPercent).HasColumnType("decimal(5,2)");
        builder.Property(o => o.TaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(o => o.OtherTaxPercent).HasColumnType("decimal(5,2)");
        builder.Property(o => o.OtherTaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(o => o.Total).HasColumnType("decimal(18,2)");

        // Relationships
        builder.HasOne(o => o.Customer)
            .WithMany(c => c.Offers)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.CreatedBy)
            .WithMany(u => u.Offers)
            .HasForeignKey(o => o.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.UpdatedBy)
            .WithMany()
            .HasForeignKey(o => o.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Lines)
            .WithOne(l => l.Offer)
            .HasForeignKey(l => l.OfferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.DeliveryBatches)
            .WithOne(b => b.Offer)
            .HasForeignKey(b => b.OfferId)
            .OnDelete(DeleteBehavior.Cascade);

        // Add these lines inside Configure(EntityTypeBuilder<Offer> builder):
        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => o.CreatedByUserId);
        builder.HasIndex(o => o.UpdatedByUserId);
    }
}