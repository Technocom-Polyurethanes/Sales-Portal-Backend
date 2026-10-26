using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Config;

public class OfferLineConfiguration : IEntityTypeConfiguration<OfferLine>
{
    public void Configure(EntityTypeBuilder<OfferLine> builder)
    {
        builder.HasKey(l => l.Id);
        builder.HasIndex(l => l.OfferId);

        builder.Property(l => l.RatePerKg).HasColumnType("decimal(18,4)");
        builder.Property(l => l.DiscountPercent).HasColumnType("decimal(5,2)");
        builder.Property(l => l.QuantityKg).HasColumnType("decimal(18,3)");
        builder.Property(l => l.Amount).HasColumnType("decimal(18,2)");

        builder.HasMany(l => l.Packaging)
            .WithOne(p => p.OfferLine)
            .HasForeignKey(p => p.OfferLineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}