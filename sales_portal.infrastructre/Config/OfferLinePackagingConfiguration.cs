using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Config;

public class OfferLinePackagingConfiguration : IEntityTypeConfiguration<OfferLinePackaging>
{
    public void Configure(EntityTypeBuilder<OfferLinePackaging> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.OfferLineId);

        builder.Property(p => p.WeightPerPackageKg).HasColumnType("decimal(18,3)");
    }
}
