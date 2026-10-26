using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Config;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CustomerCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(c => c.CustomerCode)
            .IsUnique();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(c => c.Address)
            .IsRequired();

        // FIX 1: Explicit Foreign Key relationship to PaymentTerm
        builder.HasOne(c => c.DefaultPaymentTerm)
            .WithMany()
            .HasForeignKey(c => c.DefaultPaymentTermId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}