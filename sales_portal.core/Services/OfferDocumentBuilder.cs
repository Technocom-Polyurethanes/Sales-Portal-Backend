using sales_portal.core.Calculations;
using sales_portal.core.DTOs;
using sales_portal.core.IService;

namespace sales_portal.core.Services;

public class OfferDocumentBuilder : IOfferDocumentBuilder
{
    private static readonly string[] EnglishDayNames =
    [
        "Sunday",
        "Monday",
        "Tuesday",
        "Wednesday",
        "Thursday",
        "Friday",
        "Saturday"
    ];

    private static readonly string[] ArabicDayNames =
    [
        "الأحد",
        "الإثنين",
        "الثلاثاء",
        "الأربعاء",
        "الخميس",
        "الجمعة",
        "السبت"
    ];

    public OfferDocumentDto Build(
        OfferDraftDto draft,
        OfferCatalog catalog,
        OfferCalculateResponseDto calculation)
    {
        var company = catalog.CompanySettings;
        var customer = catalog.Customer;
        var formCode = !string.IsNullOrWhiteSpace(draft.FormCode)
            ? draft.FormCode
            : company?.FormCode ?? "TF-06-01A";

        var document = new OfferDocumentDto
        {
            Header = new DocumentHeaderDto
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
            BillTo = new DocumentBillToDto
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
            Metadata = new DocumentMetadataDto
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
            Items = calculation.Lines.Select(line => new DocumentLineItemDto
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
                Packaging = line.Packaging.Select(packaging => new DocumentLinePackagingDto
                {
                    PackageTypeId = packaging.PackageTypeId,
                    PackageTypeName = packaging.PackageTypeName,
                    PackageTypeNameAr = packaging.PackageTypeNameAr,
                    Packages = packaging.Packages,
                    WeightPerPackageKg = packaging.WeightPerPackageKg,
                    RowWeightKg = packaging.RowWeightKg
                }).ToList()
            }).ToList(),
            Totals = new DocumentTotalsDto
            {
                Subtotal = calculation.Totals.Subtotal,
                OrderDiscount = calculation.Totals.OrderDiscount,
                OrderDiscountPercent = calculation.Totals.OrderDiscountPercent,
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
            DeliverySchedule = calculation.DeliveryBatches
                .OrderBy(batch => batch.BatchNumber)
                .Select(batch => BuildDeliveryBatch(batch, calculation))
                .ToList(),
            BankAccounts = catalog.BankAccounts
                .Where(account => account.IsActive)
                .OrderBy(account => account.SortOrder)
                .Select(account => new DocumentBankAccountDto
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

        document.DeliveryTotals = new DocumentDeliveryTotalsRowDto
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
                .Select(group => new DocumentDeliveryPackagingSummaryDto
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
                .ToList(),
            Materials = BuildDeliveryTotalsMaterials(calculation)
        };

        return document;
    }

    private static DocumentDeliveryBatchRowDto BuildDeliveryBatch(
        CalculatedDeliveryBatchDto batch,
        OfferCalculateResponseDto calculation)
    {
        var linesByMaterial = calculation.Lines
            .GroupBy(line => line.MaterialId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(line => line.SortOrder).ToList());
        var dayIndex = (int)batch.DeliveryDate.DayOfWeek;

        var materials = batch.Allocations
            .GroupBy(allocation => new
            {
                allocation.MaterialId,
                allocation.MaterialCode,
                allocation.MaterialName,
                allocation.Category
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

                        return new DocumentDeliveryPackagingLineDto
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

                linesByMaterial.TryGetValue(materialGroup.Key.MaterialId, out var materialLines);
                var orderedPackages = materialLines?
                    .Sum(line => line.Packaging.Sum(packaging => packaging.Packages)) ?? 0;
                var orderedWeight = materialLines?.Sum(line => line.QuantityKg) ?? 0m;

                return new DocumentDeliveryMaterialCellDto
                {
                    MaterialId = materialGroup.Key.MaterialId,
                    MaterialCode = materialGroup.Key.MaterialCode,
                    MaterialName = materialGroup.Key.MaterialName,
                    Category = materialGroup.Key.Category,
                    Packaging = packagingRows,
                    TotalPackages = packagingRows.Sum(packaging => packaging.Packages),
                    TotalWeightKg = MoneyMath.Round3(packagingRows.Sum(packaging => packaging.TotalWeightKg)),
                    OrderedPackages = orderedPackages,
                    OrderedWeightKg = orderedWeight,
                    DisplayText = string.Join(", ", packagingRows.Select(packaging => packaging.DisplayText)),
                    DisplayTextAr = string.Join(", ", packagingRows.Select(packaging => packaging.DisplayTextAr))
                };
            })
            .OrderBy(material =>
                linesByMaterial.TryGetValue(material.MaterialId, out var materialLines)
                    ? materialLines[0].SortOrder
                    : int.MaxValue)
            .ThenBy(material => material.MaterialName)
            .ToList();

        var packagingSummary = batch.PackagingSummary
            .Select(summary => new DocumentDeliveryPackagingSummaryDto
            {
                PackageTypeId = summary.PackageTypeId,
                PackageTypeName = summary.PackageTypeName,
                PackageTypeNameAr = summary.PackageTypeNameAr,
                Packages = summary.Packages,
                TotalWeightKg = summary.TotalWeightKg,
                DisplayText = $"{summary.Packages} {summary.PackageTypeName}",
                DisplayTextAr = $"{summary.Packages} {summary.PackageTypeNameAr}"
            })
            .OrderBy(summary => summary.PackageTypeName)
            .ToList();

        return new DocumentDeliveryBatchRowDto
        {
            BatchNumber = batch.BatchNumber,
            DeliveryDate = batch.DeliveryDate,
            DayOfWeek = EnglishDayNames[dayIndex],
            DayOfWeekAr = ArabicDayNames[dayIndex],
            Materials = materials,
            PackagingSummary = packagingSummary,
            TotalPackages = batch.TotalPackages,
            TotalWeightKg = batch.TotalWeightKg
        };
    }

    private static List<DocumentDeliveryMaterialCellDto> BuildDeliveryTotalsMaterials(
        OfferCalculateResponseDto calculation)
    {
        var allocations = calculation.DeliveryBatches
            .SelectMany(batch => batch.Allocations)
            .ToList();

        return calculation.Lines
            .OrderBy(line => line.SortOrder)
            .GroupBy(line => line.MaterialId)
            .Select(materialGroup =>
            {
                var line = materialGroup.First();
                var materialAllocations = allocations
                    .Where(allocation => allocation.MaterialId == line.MaterialId)
                    .ToList();

                var packagingRows = materialAllocations
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

                        return new DocumentDeliveryPackagingLineDto
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

                return new DocumentDeliveryMaterialCellDto
                {
                    MaterialId = line.MaterialId,
                    MaterialCode = line.MaterialCode,
                    MaterialName = line.MaterialName,
                    Category = line.Category,
                    Packaging = packagingRows,
                    TotalPackages = packagingRows.Sum(packaging => packaging.Packages),
                    TotalWeightKg = MoneyMath.Round3(packagingRows.Sum(packaging => packaging.TotalWeightKg)),
                    OrderedPackages = materialGroup.Sum(groupLine => groupLine.Packaging.Sum(packaging => packaging.Packages)),
                    OrderedWeightKg = materialGroup.Sum(groupLine => groupLine.QuantityKg),
                    DisplayText = string.Join(", ", packagingRows.Select(packaging => packaging.DisplayText)),
                    DisplayTextAr = string.Join(", ", packagingRows.Select(packaging => packaging.DisplayTextAr))
                };
            })
            .ToList();
    }
}
