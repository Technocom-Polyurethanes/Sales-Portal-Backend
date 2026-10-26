using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Config;

public class OfferDeliveryBatchConfiguration : IEntityTypeConfiguration<OfferDeliveryBatch>
{
    public void Configure(EntityTypeBuilder<OfferDeliveryBatch> builder)
    {
        builder.HasKey(b => b.Id);

        // FIX 2: Index on foreign key for performance
        builder.HasIndex(b => b.OfferId);

        builder.HasMany(b => b.Allocations)
            .WithOne()
            .HasForeignKey(a => a.DeliveryBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}