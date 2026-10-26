using System.Globalization;
using sales_portal.core.Calculations;
using sales_portal.core.DTOs;
using sales_portal.core.IService;
using sales_portal.domain.Enums;

namespace sales_portal.core.Services;

public class OfferCalculator : IOfferCalculator
{
    private const string OtherTaxNameAr = "ضريبة أرباح تجارية و صناعية (1%)";
    private const string OtherTaxNameEn = "Commercial and industrial profit tax (1%)";

    private readonly IOfferDocumentBuilder _documentBuilder;

    public OfferCalculator(IOfferDocumentBuilder documentBuilder)
    {
        _documentBuilder = documentBuilder;
    }

    public OfferCalculateResponseDto Calculate(OfferDraftDto draft, OfferCatalog catalog)
    {
        NormalizeIds(draft);

        var result = new OfferCalculateResponseDto { Draft = draft };

        if (catalog.Customer == null)
        {
            result.ValidationErrors.Add("Customer was not found.");
        }

        var dueDate = draft.DueDate ?? draft.IssueDate;
        if (catalog.PaymentTerm == null)
        {
            result.ValidationErrors.Add("Payment term was not found.");
        }
        else
        {
            dueDate = draft.IssueDate.AddDays(catalog.PaymentTerm.Days);
        }

        var materialsById = catalog.Materials.ToDictionary(m => m.Id);
        var packageTypesById = catalog.PackageTypes.ToDictionary(pt => pt.Id);

        // 1. Calculate each line and its packaging rows.
        var orderedLines = draft.Lines.OrderBy(l => l.SortOrder).ToList();
        for (var lineIndex = 0; lineIndex < orderedLines.Count; lineIndex++)
        {
            var draftLine = orderedLines[lineIndex];
            draftLine.SortOrder = lineIndex + 1;

            materialsById.TryGetValue(draftLine.MaterialId, out var material);
            if (material == null)
            {
                result.ValidationErrors.Add($"Material {draftLine.MaterialId} was not found on line {draftLine.SortOrder}.");
            }

            var calculatedLine = new CalculatedOfferLineDto
            {
                Id = draftLine.Id,
                SortOrder = draftLine.SortOrder,
                MaterialId = draftLine.MaterialId,
                MaterialCode = material?.ItemCode ?? string.Empty,
                MaterialName = material?.Name ?? string.Empty,
                Category = material?.Category ?? string.Empty,
                RatePerKg = draftLine.RatePerKg,
                ShowSpecifications = draftLine.ShowSpecifications,
                CustomSpecifications = draftLine.CustomSpecifications
            };

            var orderedPackaging = draftLine.Packaging.OrderBy(p => p.SortOrder).ToList();
            for (var packagingIndex = 0; packagingIndex < orderedPackaging.Count; packagingIndex++)
            {
                var draftPackaging = orderedPackaging[packagingIndex];
                draftPackaging.SortOrder = packagingIndex + 1;

                packageTypesById.TryGetValue(draftPackaging.PackageTypeId, out var packageType);
                if (packageType == null)
                {
                    result.ValidationErrors.Add(
                        $"Package type {draftPackaging.PackageTypeId} was not found on line {draftLine.SortOrder}.");
                }

                var packages = Math.Max(0, draftPackaging.Packages);
                var weightPerPackage = Math.Max(0m, draftPackaging.WeightPerPackageKg);
                var rowWeight = MoneyMath.Round3(packages * weightPerPackage);

                calculatedLine.Packaging.Add(new CalculatedLinePackagingDto
                {
                    Id = draftPackaging.Id,
                    SortOrder = draftPackaging.SortOrder,
                    PackageTypeId = draftPackaging.PackageTypeId,
                    PackageTypeName = packageType?.Name ?? string.Empty,
                    PackageTypeNameAr = packageType?.NameAr ?? string.Empty,
                    WeightPerPackageKg = weightPerPackage,
                    Packages = packages,
                    RowWeightKg = rowWeight
                });
            }

            calculatedLine.QuantityKg = MoneyMath.Round3(calculatedLine.Packaging.Sum(p => p.RowWeightKg));
            calculatedLine.Amount = MoneyMath.Round2(calculatedLine.QuantityKg * calculatedLine.RatePerKg);
            calculatedLine.PackagingText = BuildPackagingText(calculatedLine.Packaging, arabic: false);
            calculatedLine.PackagingTextAr = BuildPackagingText(calculatedLine.Packaging, arabic: true);

            result.Lines.Add(calculatedLine);
        }

        // 2. Calculate money totals.
        var subtotal = MoneyMath.Round2(result.Lines.Sum(l => l.Amount));
        var requestedDiscountPercent = Math.Max(0m, draft.OrderDiscountPercent);
        var orderDiscount = requestedDiscountPercent > 0m
            ? MoneyMath.Round2(subtotal * (requestedDiscountPercent / 100m))
            : Math.Max(0m, MoneyMath.Round2(draft.OrderDiscount));
        var effectiveDiscountPercent = requestedDiscountPercent > 0m
            ? requestedDiscountPercent
            : subtotal > 0m
                ? MoneyMath.Round2(orderDiscount / subtotal * 100m)
                : 0m;

        var netAfterDiscount = Math.Max(0m, MoneyMath.Round2(subtotal - orderDiscount));
        var taxPercent = Math.Max(0m, draft.TaxPercent);
        var taxAmount = taxPercent > 0m
            ? MoneyMath.Round2(netAfterDiscount * (taxPercent / 100m))
            : 0m;

        var otherTaxPercent = draft.OtherTaxEnabled ? 1.0m : 0m;
        var otherTaxAmount = draft.OtherTaxEnabled
            ? MoneyMath.Round2(netAfterDiscount * 0.01m)
            : 0m;
        var otherTaxName = draft.OtherTaxEnabled
            ? (draft.DocumentLanguage == DocumentLanguage.Ar ? OtherTaxNameAr : OtherTaxNameEn)
            : string.Empty;

        var total = MoneyMath.Round2(netAfterDiscount + taxAmount - otherTaxAmount);

        result.Totals = new CalculatedTotalsDto
        {
            Subtotal = subtotal,
            OrderDiscount = orderDiscount,
            OrderDiscountPercent = effectiveDiscountPercent,
            NetAfterDiscount = netAfterDiscount,
            TaxPercent = taxPercent,
            TaxAmount = taxAmount,
            OtherTaxEnabled = draft.OtherTaxEnabled,
            OtherTaxName = otherTaxName,
            OtherTaxPercent = otherTaxPercent,
            OtherTaxAmount = otherTaxAmount,
            Total = total,
            IssueDate = draft.IssueDate,
            DueDate = dueDate,
            Currency = draft.Currency,
            DocumentLanguage = draft.DocumentLanguage
        };

        draft.DueDate = dueDate;
        draft.OrderDiscount = orderDiscount;
        draft.OrderDiscountPercent = effectiveDiscountPercent;
        draft.TaxPercent = taxPercent;
        draft.OtherTaxPercent = otherTaxPercent;
        draft.OtherTaxName = otherTaxName;

        // 3. Reconcile delivery allocations against each packaging row.
        var lineLookup = result.Lines.ToDictionary(l => l.Id);
        var packagingLookup = result.Lines
            .SelectMany(line => line.Packaging.Select(packaging => new { Line = line, Packaging = packaging }))
            .ToDictionary(x => x.Packaging.Id, x => x);

        var caps = result.Lines
            .SelectMany(line => line.Packaging.Select(packaging => new DeliveryCapDto
            {
                OfferLineId = line.Id,
                OfferLinePackagingId = packaging.Id,
                MaterialId = line.MaterialId,
                MaterialCode = line.MaterialCode,
                MaterialName = line.MaterialName,
                PackageTypeId = packaging.PackageTypeId,
                PackageTypeName = packaging.PackageTypeName,
                PackageTypeNameAr = packaging.PackageTypeNameAr,
                WeightPerPackageKg = packaging.WeightPerPackageKg,
                OrderedPackages = packaging.Packages,
                ScheduledPackages = 0,
                RemainingPackages = packaging.Packages
            }))
            .ToDictionary(cap => cap.OfferLinePackagingId, cap => cap);

        var batchIndex = 0;
        foreach (var draftBatch in draft.DeliveryBatches.OrderBy(b => b.BatchNumber).ToList())
        {
            batchIndex++;
            draftBatch.BatchNumber = draftBatch.BatchNumber > 0 ? draftBatch.BatchNumber : batchIndex;

            var calculatedBatch = new CalculatedDeliveryBatchDto
            {
                Id = draftBatch.Id,
                BatchNumber = draftBatch.BatchNumber,
                DeliveryDate = draftBatch.DeliveryDate
            };

            foreach (var allocation in draftBatch.Allocations.ToList())
            {
                if (allocation.Packages <= 0)
                {
                    draftBatch.Allocations.Remove(allocation);
                    continue;
                }

                CalculatedOfferLineDto? line = null;
                CalculatedLinePackagingDto? packaging = null;

                if (allocation.OfferLinePackagingId != Guid.Empty &&
                    packagingLookup.TryGetValue(allocation.OfferLinePackagingId, out var packagingMatch))
                {
                    packaging = packagingMatch.Packaging;
                    line = packagingMatch.Line;
                }

                if (line == null && allocation.OfferLineId != Guid.Empty &&
                    lineLookup.TryGetValue(allocation.OfferLineId, out var lineMatch))
                {
                    line = lineMatch;
                }

                if (line == null && allocation.MaterialId != Guid.Empty)
                {
                    line = result.Lines.FirstOrDefault(l => l.MaterialId == allocation.MaterialId);
                }

                if (line == null)
                {
                    result.ValidationErrors.Add(
                        $"Delivery batch {draftBatch.BatchNumber} allocation does not match an offer line.");
                    continue;
                }

                if (packaging == null && allocation.PackageTypeId != Guid.Empty)
                {
                    packaging = line.Packaging.FirstOrDefault(p => p.PackageTypeId == allocation.PackageTypeId);
                }

                if (packaging == null)
                {
                    result.ValidationErrors.Add(
                        $"Delivery batch {draftBatch.BatchNumber} allocation does not match a packaging row on material {line.MaterialName}.");
                    continue;
                }

                if (allocation.OfferLineId != Guid.Empty && allocation.OfferLineId != line.Id)
                {
                    result.ValidationWarnings.Add(
                        $"Delivery batch {draftBatch.BatchNumber} allocation line was corrected to match its packaging row.");
                }

                var cap = caps[packaging.Id];
                var allowedPackages = Math.Min(allocation.Packages, cap.RemainingPackages);
                if (allowedPackages < allocation.Packages)
                {
                    result.ValidationWarnings.Add(
                        $"Delivery batch {draftBatch.BatchNumber}: {packaging.PackageTypeName} for {line.MaterialName} was reduced from {allocation.Packages} to {allowedPackages} package(s) to respect the ordered quantity.");
                }

                allocation.OfferLineId = line.Id;
                allocation.OfferLinePackagingId = packaging.Id;
                allocation.MaterialId = line.MaterialId;
                allocation.PackageTypeId = packaging.PackageTypeId;
                allocation.Packages = Math.Max(0, allowedPackages);

                if (allowedPackages <= 0)
                {
                    draftBatch.Allocations.Remove(allocation);
                    continue;
                }

                cap.ScheduledPackages += allowedPackages;
                cap.RemainingPackages -= allowedPackages;

                calculatedBatch.Allocations.Add(new CalculatedDeliveryAllocationDto
                {
                    Id = allocation.Id,
                    OfferLineId = line.Id,
                    OfferLinePackagingId = packaging.Id,
                    MaterialId = line.MaterialId,
                    MaterialCode = line.MaterialCode,
                    MaterialName = line.MaterialName,
                    Category = line.Category,
                    PackageTypeId = packaging.PackageTypeId,
                    PackageTypeName = packaging.PackageTypeName,
                    PackageTypeNameAr = packaging.PackageTypeNameAr,
                    WeightPerPackageKg = packaging.WeightPerPackageKg,
                    Packages = allowedPackages,
                    TotalWeightKg = MoneyMath.Round3(allowedPackages * packaging.WeightPerPackageKg)
                });
            }

            calculatedBatch.PackagingSummary = calculatedBatch.Allocations
                .GroupBy(a => new { a.PackageTypeId, a.PackageTypeName, a.PackageTypeNameAr })
                .Select(group => new DeliveryPackagingSummaryDto
                {
                    PackageTypeId = group.Key.PackageTypeId,
                    PackageTypeName = group.Key.PackageTypeName,
                    PackageTypeNameAr = group.Key.PackageTypeNameAr,
                    Packages = group.Sum(a => a.Packages),
                    TotalWeightKg = MoneyMath.Round3(group.Sum(a => a.TotalWeightKg))
                })
                .OrderBy(summary => summary.PackageTypeName)
                .ToList();

            calculatedBatch.TotalPackages = calculatedBatch.Allocations.Sum(a => a.Packages);
            calculatedBatch.TotalWeightKg = MoneyMath.Round3(calculatedBatch.Allocations.Sum(a => a.TotalWeightKg));

            if (calculatedBatch.Allocations.Count == 0)
            {
                draft.DeliveryBatches.Remove(draftBatch);
                continue;
            }

            result.DeliveryBatches.Add(calculatedBatch);
        }

        foreach (var line in result.Lines)
        {
            foreach (var packaging in line.Packaging)
            {
                var cap = caps[packaging.Id];
                packaging.ScheduledPackages = cap.ScheduledPackages;
                packaging.RemainingPackages = cap.RemainingPackages;
            }
        }

        result.DeliveryCaps = caps.Values
            .OrderBy(cap => cap.MaterialName)
            .ThenBy(cap => cap.PackageTypeName)
            .ToList();

        if (draft.DeliveryBatches.Count > 0)
        {
            foreach (var cap in result.DeliveryCaps.Where(cap => cap.RemainingPackages > 0))
            {
                var weight = cap.WeightPerPackageKg.ToString("0.###", CultureInfo.InvariantCulture);
                result.ValidationWarnings.Add(
                    $"{cap.MaterialName} ({cap.PackageTypeName} {weight} kg): {cap.RemainingPackages} of {cap.OrderedPackages} packages are not scheduled yet.");
            }
        }

        result.Document = _documentBuilder.Build(draft, catalog, result);
        return result;
    }

    private static void NormalizeIds(OfferDraftDto draft)
    {
        var lineIds = new HashSet<Guid>();
        var packagingIds = new HashSet<Guid>();
        var batchIds = new HashSet<Guid>();
        var allocationIds = new HashSet<Guid>();

        foreach (var line in draft.Lines)
        {
            if (line.Id == Guid.Empty || lineIds.Contains(line.Id))
            {
                line.Id = Guid.NewGuid();
            }

            lineIds.Add(line.Id);

            foreach (var packaging in line.Packaging)
            {
                if (packaging.Id == Guid.Empty || packagingIds.Contains(packaging.Id))
                {
                    packaging.Id = Guid.NewGuid();
                }

                packagingIds.Add(packaging.Id);
            }
        }

        foreach (var batch in draft.DeliveryBatches)
        {
            if (batch.Id == Guid.Empty || batchIds.Contains(batch.Id))
            {
                batch.Id = Guid.NewGuid();
            }

            batchIds.Add(batch.Id);

            foreach (var allocation in batch.Allocations)
            {
                if (allocation.Id == Guid.Empty || allocationIds.Contains(allocation.Id))
                {
                    allocation.Id = Guid.NewGuid();
                }

                allocationIds.Add(allocation.Id);
            }
        }
    }

    private static string BuildPackagingText(IReadOnlyList<CalculatedLinePackagingDto> rows, bool arabic)
    {
        var parts = rows
            .Where(row => row.Packages > 0)
            .OrderBy(row => row.SortOrder)
            .Select(row =>
            {
                var weight = row.WeightPerPackageKg.ToString("0.###", CultureInfo.InvariantCulture);
                return arabic
                    ? $"{row.Packages} {row.PackageTypeNameAr} × {weight} كجم"
                    : $"{row.Packages} {row.PackageTypeName} × {weight} kg";
            })
            .ToList();

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        var totalWeight = rows.Sum(row => row.RowWeightKg).ToString("N2", CultureInfo.InvariantCulture);
        return string.Join(" + ", parts) + (arabic ? $" = {totalWeight} كجم" : $" = {totalWeight} kg");
    }
}
