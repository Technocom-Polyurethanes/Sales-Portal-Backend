using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Config;

public class OfferDeliveryBatchConfiguration : IEntityTypeConfiguration<OfferDeliveryBatch>
{
    public void Configure(EntityTypeBuilder<OfferDeliveryBatch> builder)
    {
        builder.HasKey(b => b.Id);

        // Index on foreign key for performance
        builder.HasIndex(b => b.OfferId);

        builder.HasMany(b => b.Allocations)
            .WithOne(a => a.DeliveryBatch)
            .HasForeignKey(a => a.DeliveryBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OfferDeliveryAllocationConfiguration : IEntityTypeConfiguration<OfferDeliveryAllocation>
{
    public void Configure(EntityTypeBuilder<OfferDeliveryAllocation> builder)
    {
        builder.HasKey(a => a.Id);

        builder.HasIndex(a => a.DeliveryBatchId);
        builder.HasIndex(a => a.OfferLineId);
        builder.HasIndex(a => a.OfferLinePackagingId);

        builder.Property(a => a.WeightPerPackageKg).HasColumnType("decimal(18,3)");
        builder.Property(a => a.TotalWeightKg).HasColumnType("decimal(18,3)");

        builder.HasOne(a => a.OfferLine)
            .WithMany()
            .HasForeignKey(a => a.OfferLineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.OfferLinePackaging)
            .WithMany()
            .HasForeignKey(a => a.OfferLinePackagingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
