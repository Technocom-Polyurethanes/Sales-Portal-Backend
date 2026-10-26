using sales_portal.core.Calculations;
using sales_portal.core.DTOs;

namespace sales_portal.core.Services;

public class OfferDocumentBuilder
{
    public OfferDocumentDto Build(OfferDraftDto draft, OfferCatalog catalog, OfferCalculationResultDto calculation)
    {
        var company = catalog.CompanySettings;
        var customer = catalog.Customer;
        var formCode = !string.IsNullOrWhiteSpace(draft.FormCode)
            ? draft.FormCode
            : company?.FormCode ?? "TF-06-01A";

        var document = new OfferDocumentDto
        {
            Header = new OfferDocumentHeaderDto
            {
                CompanyName = company?.Name ?? string.Empty,
                CompanyNameAr = company?.NameAr ?? string.Empty,
                Tagline = company?.Tagline ?? string.Empty,
                TaglineAr = company?.TaglineAr ?? string.Empty,
                Address = company?.Address,
                Phone = company?.Phone,
                Email = company?.Email,
                LogoUrl = company?.LogoUrl,
                LogoWidthMm = company?.LogoWidthMm ?? 0m,
                LogoHeightMm = company?.LogoHeightMm ?? 0m,
                DocumentTitle = "SALES OFFER",
                DocumentTitleAr = "عرض سعر",
                FormCode = formCode,
                Reference = draft.Reference,
                Version = draft.Version
            },
            BillTo = new OfferDocumentCustomerDto
            {
                CustomerId = customer?.Id ?? draft.CustomerId,
                CustomerCode = customer?.CustomerCode ?? string.Empty,
                Name = customer?.Name ?? string.Empty,
                NameAr = customer?.NameAr,
                Address = customer?.Address ?? string.Empty,
                City = customer?.City,
                ContactPerson = customer?.ContactPerson,
                Attention = !string.IsNullOrWhiteSpace(draft.Attention) ? draft.Attention : customer?.ContactPerson,
                TaxId = !string.IsNullOrWhiteSpace(draft.TaxId) ? draft.TaxId : customer?.TaxId,
                Email = customer?.Email,
                Phone = customer?.Phone
            },
            Metadata = new OfferDocumentMetadataDto
            {
                Reference = draft.Reference,
                SerialNumber = draft.SerialNumber,
                Version = draft.Version,
                IssueDate = calculation.Totals.IssueDate,
                DueDate = calculation.Totals.DueDate,
                PaymentTerm = catalog.PaymentTerm?.Label ?? string.Empty,
                PaymentTermAr = catalog.PaymentTerm?.LabelAr ?? string.Empty,
                SalesRepresentative = catalog.CreatedBy?.FullName ?? string.Empty,
                SalesRepresentativeInitials = catalog.CreatedBy?.Initials ?? string.Empty,
                Status = draft.Status,
                Currency = draft.Currency,
                FormCode = formCode
            },
            Items = calculation.Lines.Select(line => new OfferDocumentLineDto
            {
                OfferLineId = line.Id,
                SortOrder = line.SortOrder,
                MaterialId = line.MaterialId,
                MaterialCode = line.MaterialCode,
                MaterialName = line.MaterialName,
                Category = line.Category,
                QuantityKg = line.QuantityKg,
                RatePerKg = line.RatePerKg,
                Amount = line.Amount,
                ShowSpecifications = line.ShowSpecifications,
                CustomSpecifications = line.CustomSpecifications,
                PackagingText = line.PackagingText,
                PackagingTextAr = line.PackagingTextAr,
                Packaging = line.Packaging.Select(packaging => new OfferDocumentPackagingRowDto
                {
                    PackageTypeId = packaging.PackageTypeId,
                    PackageTypeName = packaging.PackageTypeName,
                    PackageTypeNameAr = packaging.PackageTypeNameAr,
                    Packages = packaging.Packages,
                    WeightPerPackageKg = packaging.WeightPerPackageKg,
                    RowWeightKg = packaging.RowWeightKg
                }).ToList()
            }).ToList(),
            Totals = new OfferDocumentTotalsDto
            {
                Subtotal = calculation.Totals.Subtotal,
                OrderDiscount = calculation.Totals.OrderDiscount,
                NetAfterDiscount = calculation.Totals.NetAfterDiscount,
                TaxPercent = calculation.Totals.TaxPercent,
                TaxAmount = calculation.Totals.TaxAmount,
                OtherTaxEnabled = calculation.Totals.OtherTaxEnabled,
                OtherTaxName = calculation.Totals.OtherTaxName,
                OtherTaxPercent = calculation.Totals.OtherTaxPercent,
                OtherTaxAmount = calculation.Totals.OtherTaxAmount,
                Total = calculation.Totals.Total,
                Currency = calculation.Totals.Currency
            },
            DeliverySchedule = calculation.DeliveryBatches.Select(batch => BuildDeliveryBatch(batch)).ToList(),
            BankAccounts = catalog.BankAccounts
                .Where(account => account.IsActive)
                .OrderBy(account => account.SortOrder)
                .Select(account => new OfferDocumentBankAccountDto
                {
                    Bank = account.Bank,
                    Branch = account.Branch,
                    AccountHolderName = account.AccountHolderName,
                    AccountNumber = account.AccountNumber,
                    Swift = account.Swift,
                    Iban = account.Iban,
                    Currency = account.Currency
                })
                .ToList(),
            Notes = draft.Notes,
            Language = draft.DocumentLanguage,
            Currency = draft.Currency,
            FormCode = formCode,
            GeneratedAt = DateTimeOffset.UtcNow
        };

        document.DeliveryTotals = new OfferDocumentDeliveryTotalsDto
        {
            TotalPackages = document.DeliverySchedule.Sum(batch => batch.TotalPackages),
            TotalWeightKg = MoneyMath.Round3(document.DeliverySchedule.Sum(batch => batch.TotalWeightKg)),
            PackagingSummary = calculation.DeliveryBatches
                .SelectMany(batch => batch.PackagingSummary)
                .GroupBy(summary => new
                {
                    summary.PackageTypeId,
                    summary.PackageTypeName,
                    summary.PackageTypeNameAr
                })
                .Select(group => new OfferDocumentPackagingSummaryDto
                {
                    PackageTypeId = group.Key.PackageTypeId,
                    PackageTypeName = group.Key.PackageTypeName,
                    PackageTypeNameAr = group.Key.PackageTypeNameAr,
                    Packages = group.Sum(summary => summary.Packages),
                    TotalWeightKg = MoneyMath.Round3(group.Sum(summary => summary.TotalWeightKg)),
                    DisplayText = $"{group.Sum(summary => summary.Packages)} {group.Key.PackageTypeName}",
                    DisplayTextAr = $"{group.Sum(summary => summary.Packages)} {group.Key.PackageTypeNameAr}"
                })
                .OrderBy(summary => summary.PackageTypeName)
                .ToList()
        };

        return document;
    }

    private static OfferDocumentDeliveryBatchDto BuildDeliveryBatch(CalculatedDeliveryBatchDto batch)
    {
        var materials = batch.Allocations
            .GroupBy(allocation => new
            {
                allocation.MaterialId,
                allocation.MaterialCode,
                allocation.MaterialName
            })
            .Select(materialGroup =>
            {
                var packagingRows = materialGroup
                    .GroupBy(allocation => new
                    {
                        allocation.OfferLinePackagingId,
                        allocation.PackageTypeId,
                        allocation.PackageTypeName,
                        allocation.PackageTypeNameAr,
                        allocation.WeightPerPackageKg
                    })
                    .Select(packagingGroup =>
                    {
                        var packages = packagingGroup.Sum(allocation => allocation.Packages);
                        var totalWeight = MoneyMath.Round3(packagingGroup.Sum(allocation => allocation.TotalWeightKg));

                        return new OfferDocumentScheduledPackagingDto
                        {
                            PackageTypeId = packagingGroup.Key.PackageTypeId,
                            PackageTypeName = packagingGroup.Key.PackageTypeName,
                            PackageTypeNameAr = packagingGroup.Key.PackageTypeNameAr,
                            Packages = packages,
                            WeightPerPackageKg = packagingGroup.Key.WeightPerPackageKg,
                            TotalWeightKg = totalWeight,
                            DisplayText = $"{packages} {packagingGroup.Key.PackageTypeName}",
                            DisplayTextAr = $"{packages} {packagingGroup.Key.PackageTypeNameAr}"
                        };
                    })
                    .OrderBy(packaging => packaging.PackageTypeName)
                    .ToList();

                return new OfferDocumentDeliveryMaterialDto
                {
                    MaterialId = materialGroup.Key.MaterialId,
                    MaterialCode = materialGroup.Key.MaterialCode,
                    MaterialName = materialGroup.Key.MaterialName,
                    Packaging = packagingRows,
                    TotalPackages = packagingRows.Sum(packaging => packaging.Packages),
                    TotalWeightKg = MoneyMath.Round3(packagingRows.Sum(packaging => packaging.TotalWeightKg))
                };
            })
            .OrderBy(material => material.MaterialName)
            .ToList();

        var packagingSummary = batch.PackagingSummary
            .Select(summary => new OfferDocumentPackagingSummaryDto
            {
                PackageTypeId = summary.PackageTypeId,
                PackageTypeName = summary.PackageTypeName,
                PackageTypeNameAr = summary.PackageTypeNameAr,
                Packages = summary.Packages,
                TotalWeightKg = summary.TotalWeightKg,
                DisplayText = $"{summary.Packages} {summary.PackageTypeName}",
                DisplayTextAr = $"{summary.Packages} {summary.PackageTypeNameAr}"
            })
            .ToList();

        return new OfferDocumentDeliveryBatchDto
        {
            BatchNumber = batch.BatchNumber,
            DeliveryDate = batch.DeliveryDate,
            Materials = materials,
            PackagingSummary = packagingSummary,
            TotalPackages = batch.TotalPackages,
            TotalWeightKg = batch.TotalWeightKg
        };
    }
}
