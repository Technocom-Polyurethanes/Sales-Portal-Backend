using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Config;

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.ItemCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(m => m.ItemCode)
            .IsUnique();

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(m => m.ReferencePricePerKg)
            .HasColumnType("decimal(18,4)");

        builder.Property(m => m.DefaultWeightKg)
            .HasColumnType("decimal(18,3)");
    }
}