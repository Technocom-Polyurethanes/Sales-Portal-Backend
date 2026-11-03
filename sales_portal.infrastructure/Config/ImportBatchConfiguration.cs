using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Config;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.HasKey(batch => batch.Id);

        builder.Property(batch => batch.Type)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(batch => batch.FileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(batch => batch.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(batch => batch.CreatedByName)
            .HasMaxLength(250);

        builder.HasIndex(batch => batch.CommittedAt);
    }
}
