using Microsoft.EntityFrameworkCore;
using sales_portal.core.DTOs;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using Xunit;

namespace SalesPortal.Tests;

public class OfferListTests
{
    private static OfferDraftDto BuildMinimalDraft(
        Guid customerId,
        Guid materialId,
        Guid packageTypeId,
        Guid paymentTermId,
        string reference,
        OfferType offerType,
        DateOnly issueDate,
        OfferStatus status,
        decimal quantity = 10m,
        decimal unitPrice = 100m,
        List<Guid>? bankAccountIds = null)
    {
        return new OfferDraftDto
        {
            Reference = reference,
            OfferType = offerType,
            CustomerId = customerId,
            IssueDate = issueDate,
            ValidityDays = 15,
            PaymentTermId = paymentTermId,
            Currency = CurrencyCode.Egp,
            Status = status,
            BankAccountIds = bankAccountIds,
            Lines =
            [
                new OfferDraftLineDto
                {
                    MaterialId = materialId,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    Packaging =
                    [
                        new OfferDraftPackagingDto
                        {
                            PackageTypeId = packageTypeId,
                            PackagesCount = 1,
                            WeightPerPackage = quantity
                        }
                    ]
                }
            ]
        };
    }

    [Fact]
    public async Task ReviseAsync_keeps_same_reference_and_issue_date_passes_validation()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);

        var offer = await CreateAsync(db, basics, "Loc/100/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 15), OfferStatus.Issued);
        var service = TestFactory.CreateOfferService(db.Context);

        var revised = await service.ReviseAsync(offer.Id, basics.User.Id, CancellationToken.None);

        Assert.Equal(2, revised.Version);
        Assert.Equal(offer.Reference, revised.Reference);
        Assert.Equal(offer.IssueDate, revised.IssueDate);
        Assert.Equal(OfferStatus.Draft, revised.Status);
    }

    [Fact]
    public async Task LatestOnly_returns_only_the_highest_version_per_reference()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);

        var offer1V1 = await CreateAsync(db, basics, "Loc/101/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 1), OfferStatus.Issued);
        var service = TestFactory.CreateOfferService(db.Context);
        var offer1V2 = await service.ReviseAsync(offer1V1.Id, basics.User.Id, CancellationToken.None);
        await CreateAsync(db, basics, "Loc/102/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 2), OfferStatus.Draft);

        var result = await service.GetListAsync(
            new OfferQueryParamsDto { LatestOnly = true, Page = 1, PageSize = 20 },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.DoesNotContain(result.Items, item => item.Id == offer1V1.Id);
        Assert.Contains(result.Items, item => item.Id == offer1V2.Id && item.Version == 2 && item.VersionsCount == 2);
    }

    [Fact]
    public async Task LatestOnly_false_returns_every_version()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);

        var offer1V1 = await CreateAsync(db, basics, "Loc/103/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 1), OfferStatus.Issued);
        var service = TestFactory.CreateOfferService(db.Context);
        var offer1V2 = await service.ReviseAsync(offer1V1.Id, basics.User.Id, CancellationToken.None);

        var result = await service.GetListAsync(
            new OfferQueryParamsDto { LatestOnly = false, Page = 1, PageSize = 20 },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, item => item.Id == offer1V1.Id && item.Version == 1 && item.VersionsCount == 2);
        Assert.Contains(result.Items, item => item.Id == offer1V2.Id && item.Version == 2 && item.VersionsCount == 2);
    }

    [Fact]
    public async Task Paging_counts_latest_version_rows_only()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);

        var v1 = await CreateAsync(db, basics, "Loc/104/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 1), OfferStatus.Issued);
        var service = TestFactory.CreateOfferService(db.Context);
        await service.ReviseAsync(v1.Id, basics.User.Id, CancellationToken.None);
        await CreateAsync(db, basics, "Loc/105/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 2), OfferStatus.Issued);

        var page1 = await service.GetListAsync(
            new OfferQueryParamsDto { LatestOnly = true, Page = 1, PageSize = 1 },
            CancellationToken.None);

        Assert.Single(page1.Items);
        Assert.Equal(2, page1.TotalCount);
    }

    [Fact]
    public async Task Sorting_by_updatedAt_moves_a_status_update_to_the_top()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);

        var first = await CreateAsync(db, basics, "Loc/106/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 1), OfferStatus.Draft);
        var second = await CreateAsync(db, basics, "Loc/107/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 2), OfferStatus.Draft);

        var existingFirst = await db.Context.Offers.SingleAsync(o => o.Id == first.Id);
        existingFirst.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        await db.Context.SaveChangesAsync();

        var existingSecond = await db.Context.Offers.SingleAsync(o => o.Id == second.Id);
        existingSecond.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await db.Context.SaveChangesAsync();

        var service = TestFactory.CreateOfferService(db.Context);
        await service.UpdateStatusAsync(
            first.Id,
            new UpdateOfferStatusDto { Status = OfferStatus.Issued },
            basics.User.Id,
            CancellationToken.None);

        var list = await service.GetListAsync(
            new OfferQueryParamsDto { SortBy = "updatedAt", SortDir = "desc", Page = 1, PageSize = 10 },
            CancellationToken.None);

        Assert.Equal(first.Id, list.Items[0].Id);
    }

    [Fact]
    public async Task Detail_reports_creator_modifier_and_version_count()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);

        var v1 = await CreateAsync(db, basics, "Loc/108/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 1), OfferStatus.Draft);
        var service = TestFactory.CreateOfferService(db.Context);
        var v2 = await service.ReviseAsync(v1.Id, basics.SecondUser.Id, CancellationToken.None);

        var detail = await service.GetByIdAsync(v2.Id, CancellationToken.None);

        Assert.Equal(basics.User.Id, detail.CreatedByUserId);
        Assert.Equal(basics.User.FullName, detail.CreatedByName);
        Assert.Equal(basics.SecondUser.Id, detail.UpdatedByUserId);
        Assert.Equal(basics.SecondUser.FullName, detail.UpdatedByName);
        Assert.Equal(2, detail.VersionsCount);
    }

    [Fact]
    public async Task ReviseAsync_by_another_user_keeps_the_creator_and_only_changes_the_editor()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);

        var v1 = await CreateAsync(db, basics, "Loc/109/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 1), OfferStatus.Draft);
        var service = TestFactory.CreateOfferService(db.Context);
        var revised = await service.ReviseAsync(v1.Id, basics.SecondUser.Id, CancellationToken.None);

        Assert.Equal(basics.User.Id, revised.CreatedByUserId);
        Assert.Equal(basics.User.FullName, revised.CreatedByName);
        Assert.Equal(basics.SecondUser.Id, revised.UpdatedByUserId);
        Assert.Equal(basics.SecondUser.FullName, revised.UpdatedByName);
    }

    [Fact]
    public async Task ReviseAsync_keeps_the_creation_date_of_the_first_version_and_stamps_a_new_modified_date()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var v1 = await CreateAsync(db, basics, "Loc/100/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 15), OfferStatus.Draft);

        var service = TestFactory.CreateOfferService(db.Context);
        var v2 = await service.ReviseAsync(v1.Id, basics.User.Id, CancellationToken.None);
        var v3 = await service.ReviseAsync(v2.Id, basics.User.Id, CancellationToken.None);

        Assert.Equal(v1.CreatedAt, v2.CreatedAt);
        Assert.Equal(v1.CreatedAt, v3.CreatedAt);
        Assert.True(v3.UpdatedAt >= v3.CreatedAt);
        Assert.True(v3.UpdatedAt > v1.CreatedAt);
    }
}

public class CustomerOfferGroupingTests
{
    [Fact]
    public async Task Customer_offers_list_each_offer_once_with_its_latest_version()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);

        var v1 = await OfferListTestsHelper.CreateOfferAsync(db, basics, "Loc/200/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 1), OfferStatus.Issued, 10m, 100m);
        var offerService = TestFactory.CreateOfferService(db.Context);
        var v2 = await offerService.ReviseAsync(v1.Id, basics.User.Id, CancellationToken.None);
        await offerService.UpdateAsync(
            v2.Id,
            new OfferDraftDto
            {
                Reference = v2.Reference,
                OfferType = OfferType.Loc,
                CustomerId = basics.Customer.Id,
                IssueDate = v2.IssueDate,
                ValidityDays = 15,
                PaymentTermId = basics.PaymentTerm.Id,
                Currency = CurrencyCode.Egp,
                Status = OfferStatus.Draft,
                BankAccountIds = v2.BankAccountIds,
                Lines =
                [
                    new OfferDraftLineDto
                    {
                        MaterialId = basics.Material.Id,
                        Quantity = 20m,
                        UnitPrice = 100m,
                        Packaging =
                        [
                            new OfferDraftPackagingDto
                            {
                                PackageTypeId = basics.PackageType.Id,
                                PackagesCount = 1,
                                WeightPerPackage = 20m
                            }
                        ]
                    }
                ]
            },
            basics.User.Id,
            CancellationToken.None);

        var customerService = TestFactory.CreateCustomerService(db.Context);
        var page = await customerService.GetOffersAsync(basics.Customer.Id, 1, 20, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(v2.Id, page.Items[0].Id);
        Assert.Equal(2, page.Items[0].Version);
        Assert.Equal(2, page.Items[0].VersionsCount);
    }

    [Fact]
    public async Task Customer_detail_counts_and_sums_only_the_latest_version_of_each_offer()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);

        var chain1V1 = await OfferListTestsHelper.CreateOfferAsync(db, basics, "Loc/201/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 1), OfferStatus.Issued, 10m, 100m);
        var offerService = TestFactory.CreateOfferService(db.Context);
        var chain1V2 = await offerService.ReviseAsync(chain1V1.Id, basics.User.Id, CancellationToken.None);
        await offerService.UpdateAsync(
            chain1V2.Id,
            new OfferDraftDto
            {
                Reference = chain1V2.Reference,
                OfferType = OfferType.Loc,
                CustomerId = basics.Customer.Id,
                IssueDate = chain1V2.IssueDate,
                ValidityDays = 15,
                PaymentTermId = basics.PaymentTerm.Id,
                Currency = CurrencyCode.Egp,
                Status = OfferStatus.Draft,
                BankAccountIds = chain1V2.BankAccountIds,
                Lines =
                [
                    new OfferDraftLineDto
                    {
                        MaterialId = basics.Material.Id,
                        Quantity = 5m,
                        UnitPrice = 100m,
                        Packaging =
                        [
                            new OfferDraftPackagingDto
                            {
                                PackageTypeId = basics.PackageType.Id,
                                PackagesCount = 1,
                                WeightPerPackage = 5m
                            }
                        ]
                    }
                ]
            },
            basics.User.Id,
            CancellationToken.None);

        await OfferListTestsHelper.CreateOfferAsync(db, basics, "Loc/202/YL/AA", OfferType.Loc, new DateOnly(2026, 10, 5), OfferStatus.Issued, 10m, 100m);

        var customerService = TestFactory.CreateCustomerService(db.Context);
        var detail = await customerService.GetByIdAsync(basics.Customer.Id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(2, detail!.OffersCount);
        Assert.Equal(2, detail.RecentOffers.Count);
        Assert.Equal(1500m, detail.TotalValue);
    }
}

internal static class OfferListTestsHelper
{
    public static Task<OfferDetailDto> CreateOfferAsync(
        TestDatabase db,
        TestBasics basics,
        string reference,
        OfferType offerType,
        DateOnly issueDate,
        OfferStatus status,
        decimal quantity = 10m,
        decimal unitPrice = 100m,
        List<Guid>? bankAccountIds = null)
    {
        return OfferListTestsBridge.CreateAsync(db, basics, reference, offerType, issueDate, status, quantity, unitPrice, bankAccountIds);
    }
}

file static class OfferListTestsBridge
{
    public static async Task<OfferDetailDto> CreateAsync(
        TestDatabase db,
        TestBasics basics,
        string reference,
        OfferType offerType,
        DateOnly issueDate,
        OfferStatus status,
        decimal quantity,
        decimal unitPrice,
        List<Guid>? bankAccountIds)
    {
        var service = TestFactory.CreateOfferService(db.Context);
        var draft = new OfferDraftDto
        {
            Reference = reference,
            OfferType = offerType,
            CustomerId = basics.Customer.Id,
            IssueDate = issueDate,
            ValidityDays = 15,
            PaymentTermId = basics.PaymentTerm.Id,
            Currency = CurrencyCode.Egp,
            Status = OfferStatus.Draft,
            BankAccountIds = bankAccountIds,
            Lines =
            [
                new OfferDraftLineDto
                {
                    MaterialId = basics.Material.Id,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    Packaging =
                    [
                        new OfferDraftPackagingDto
                        {
                            PackageTypeId = basics.PackageType.Id,
                            PackagesCount = 1,
                            WeightPerPackage = quantity
                        }
                    ]
                }
            ]
        };

        var created = await service.CreateAsync(draft, basics.User.Id, CancellationToken.None);
        if (status != OfferStatus.Draft)
        {
            return await service.UpdateStatusAsync(
                created.Id,
                new UpdateOfferStatusDto { Status = status },
                basics.User.Id,
                CancellationToken.None);
        }

        return created;
    }
}

file static class OfferListTestsExtensions
{
    public static Task<OfferDetailDto> CreateAsync(
        this OfferListTests _,
        TestDatabase db,
        TestBasics basics,
        string reference,
        OfferType offerType,
        DateOnly issueDate,
        OfferStatus status,
        decimal quantity = 10m,
        decimal unitPrice = 100m,
        List<Guid>? bankAccountIds = null)
    {
        return OfferListTestsBridge.CreateAsync(db, basics, reference, offerType, issueDate, status, quantity, unitPrice, bankAccountIds);
    }
}
