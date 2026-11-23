using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using Xunit;

namespace SalesPortal.Tests;

public class OfferListTests
{
    [Fact]
    public async Task LatestOnly_returns_only_the_highest_version_per_reference()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1);
        TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 2);
        TestFactory.AddOffer(db.Context, basics, "Loc/101/YL/AA", version: 1);

        var service = TestFactory.CreateOfferService(db.Context);
        var result = await service.GetListAsync(new OfferQueryParamsDto(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        var offer100 = Assert.Single(result.Items, item => item.Reference == "Loc/100/YL/AA");
        Assert.Equal(2, offer100.Version);
        Assert.Equal(2, offer100.VersionsCount);
        Assert.Equal(1, result.Items.Single(item => item.Reference == "Loc/101/YL/AA").VersionsCount);
    }

    [Fact]
    public async Task LatestOnly_false_returns_every_version()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1);
        TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 2);

        var service = TestFactory.CreateOfferService(db.Context);
        var result = await service.GetListAsync(
            new OfferQueryParamsDto { LatestOnly = false },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(2, item.VersionsCount));
    }

    [Fact]
    public async Task Sorting_by_updatedAt_moves_a_status_update_to_the_top()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var older = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1, status: OfferStatus.Issued);
        TestFactory.AddOffer(db.Context, basics, "Loc/101/YL/AA", version: 1);

        var service = TestFactory.CreateOfferService(db.Context);
        var byUpdatedAt = new OfferQueryParamsDto { SortBy = "updatedAt" };
        var before = await service.GetListAsync(byUpdatedAt, CancellationToken.None);
        Assert.Equal(2, before.Items.Count);

        await service.UpdateStatusAsync(
            older.Id,
            new UpdateOfferStatusDto { Status = OfferStatus.Accepted },
            basics.SecondUser.Id,
            CancellationToken.None);

        var after = await service.GetListAsync(byUpdatedAt, CancellationToken.None);
        Assert.Equal(older.Id, after.Items[0].Id);
        Assert.Equal(basics.SecondUser.FullName, after.Items[0].LastModifiedByName);
        Assert.True(after.Items[0].LastModifiedAt >= after.Items[1].LastModifiedAt);
    }

    [Fact]
    public async Task Status_filter_applies_to_latest_version_rows()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1, status: OfferStatus.Issued);
        TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 2, status: OfferStatus.Draft);
        TestFactory.AddOffer(db.Context, basics, "Loc/101/YL/AA", version: 1, status: OfferStatus.Issued);

        var service = TestFactory.CreateOfferService(db.Context);

        var issued = await service.GetListAsync(
            new OfferQueryParamsDto { Status = OfferStatus.Issued },
            CancellationToken.None);

        var item = Assert.Single(issued.Items);
        Assert.Equal("Loc/101/YL/AA", item.Reference);
        Assert.Equal(1, issued.TotalCount);
    }

    [Fact]
    public async Task Paging_counts_latest_version_rows_only()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        for (var i = 0; i < 3; i++)
        {
            var reference = $"Loc/{200 + i}/YL/AA";
            TestFactory.AddOffer(db.Context, basics, reference, version: 1);
            TestFactory.AddOffer(db.Context, basics, reference, version: 2);
        }

        var service = TestFactory.CreateOfferService(db.Context);
        var firstPage = await service.GetListAsync(
            new OfferQueryParamsDto { Page = 1, PageSize = 2 },
            CancellationToken.None);
        var secondPage = await service.GetListAsync(
            new OfferQueryParamsDto { Page = 2, PageSize = 2 },
            CancellationToken.None);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Single(secondPage.Items);
    }

    [Fact]
    public async Task Versions_endpoint_returns_the_same_list_fields_newest_first()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var v1 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1);
        var v2 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 2);

        var service = TestFactory.CreateOfferService(db.Context);
        var versions = await service.GetVersionsAsync(v1.Id, CancellationToken.None);

        Assert.Equal(2, versions.Count);
        Assert.Equal(v2.Id, versions[0].Id);
        Assert.Equal(v1.Id, versions[1].Id);
        Assert.All(versions, version => Assert.Equal(2, version.VersionsCount));
    }

    [Fact]
    public async Task Detail_reports_creator_modifier_and_version_count()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1);
        var v2 = TestFactory.AddOffer(
            db.Context,
            basics,
            "Loc/100/YL/AA",
            version: 2,
            updatedByUserId: basics.SecondUser.Id);

        var service = TestFactory.CreateOfferService(db.Context);
        var detail = await service.GetByIdAsync(v2.Id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(basics.User.FullName, detail.CreatedByName);
        Assert.Equal(basics.SecondUser.FullName, detail.UpdatedByName);
        Assert.Equal(2, detail.VersionsCount);
    }

    [Fact]
    public async Task ReviseAsync_keeps_same_reference_and_issue_date_passes_validation()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var v1 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1, issueDate: new DateOnly(2026, 10, 15));

        var service = TestFactory.CreateOfferService(db.Context);
        var revised = await service.ReviseAsync(v1.Id, basics.User.Id, CancellationToken.None);

        Assert.NotNull(revised);
        Assert.Equal("Loc/100/YL/AA", revised.Reference);
        Assert.Equal(2, revised.Version);
        Assert.Equal(new DateOnly(2026, 10, 15), revised.IssueDate);
    }

    [Fact]
    public async Task ReviseAsync_by_another_user_keeps_the_creator_and_only_changes_the_editor()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var v1 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1);

        var service = TestFactory.CreateOfferService(db.Context);
        var revised = await service.ReviseAsync(v1.Id, basics.SecondUser.Id, CancellationToken.None);

        Assert.Equal(basics.User.Id, revised.CreatedByUserId);
        Assert.Equal(basics.User.FullName, revised.CreatedByName);
        Assert.Equal(basics.SecondUser.Id, revised.UpdatedByUserId);
        Assert.Equal(basics.SecondUser.FullName, revised.UpdatedByName);
    }

    [Fact]
    public async Task ReviseAsync_gives_each_version_its_own_creation_date_and_keeps_the_first_as_root()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        SeedBankAccount(db);
        var v1 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1);
        var firstCreatedAt = SetCreatedAt(db, v1, new DateTimeOffset(2020, 10, 15, 9, 0, 0, TimeSpan.Zero));

        var service = TestFactory.CreateOfferService(db.Context);
        var v2 = await service.ReviseAsync(v1.Id, basics.User.Id, CancellationToken.None);
        var v3 = await service.ReviseAsync(v2.Id, basics.User.Id, CancellationToken.None);

        // A revision is stamped when it is created, not with the date of an earlier version.
        Assert.True(v2.CreatedAt > firstCreatedAt);
        Assert.True(v3.CreatedAt >= v2.CreatedAt);
        Assert.True(v3.UpdatedAt >= v3.CreatedAt);

        // The offer's creation date stays that of v1 on every version.
        Assert.Equal(firstCreatedAt, v2.RootCreatedAt);
        Assert.Equal(firstCreatedAt, v3.RootCreatedAt);

        // v1 itself is untouched.
        var reloadedV1 = await service.GetByIdAsync(v1.Id, CancellationToken.None);
        Assert.NotNull(reloadedV1);
        Assert.Equal(firstCreatedAt, reloadedV1!.CreatedAt);
        Assert.Equal(firstCreatedAt, reloadedV1.RootCreatedAt);
    }

    [Fact]
    public async Task Updating_an_offer_never_changes_its_creation_date()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var v1 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1);
        var createdAt = SetCreatedAt(db, v1, new DateTimeOffset(2020, 10, 15, 9, 0, 0, TimeSpan.Zero));

        var service = TestFactory.CreateOfferService(db.Context);
        var updated = await service.UpdateStatusAsync(
            v1.Id,
            new UpdateOfferStatusDto { Status = OfferStatus.Accepted },
            basics.SecondUser.Id,
            CancellationToken.None);

        Assert.Equal(createdAt, updated.CreatedAt);
        Assert.Equal(createdAt, updated.RootCreatedAt);
        Assert.True(updated.UpdatedAt > createdAt);
        Assert.Equal(basics.SecondUser.FullName, updated.UpdatedByName);
    }

    [Fact]
    public async Task Updating_a_later_version_keeps_its_own_creation_date_and_the_root_date()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var v1 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1);
        var v2 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 2);
        var firstCreatedAt = SetCreatedAt(db, v1, new DateTimeOffset(2020, 10, 15, 9, 0, 0, TimeSpan.Zero));
        var secondCreatedAt = SetCreatedAt(db, v2, new DateTimeOffset(2020, 10, 16, 10, 30, 0, TimeSpan.Zero));

        var service = TestFactory.CreateOfferService(db.Context);
        var updated = await service.UpdateStatusAsync(
            v2.Id,
            new UpdateOfferStatusDto { Status = OfferStatus.Accepted },
            basics.SecondUser.Id,
            CancellationToken.None);

        Assert.Equal(secondCreatedAt, updated.CreatedAt);
        Assert.Equal(firstCreatedAt, updated.RootCreatedAt);
        Assert.True(updated.UpdatedAt > secondCreatedAt);
    }

    [Fact]
    public async Task Detail_versions_and_list_expose_the_first_versions_creation_date()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var v1 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1);
        var v2 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 2);
        var v3 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 3);
        var firstCreatedAt = SetCreatedAt(db, v1, new DateTimeOffset(2020, 10, 15, 9, 5, 0, TimeSpan.Zero));
        var secondCreatedAt = SetCreatedAt(db, v2, new DateTimeOffset(2020, 10, 16, 10, 23, 0, TimeSpan.Zero));
        var thirdCreatedAt = SetCreatedAt(db, v3, new DateTimeOffset(2020, 10, 17, 11, 41, 0, TimeSpan.Zero));

        var service = TestFactory.CreateOfferService(db.Context);

        // Offer summary: viewing the newest version still reports the first creation date.
        var detail = await service.GetByIdAsync(v3.Id, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.Equal(thirdCreatedAt, detail!.CreatedAt);
        Assert.Equal(firstCreatedAt, detail.RootCreatedAt);

        // Version history: every row has its own creation date and the shared root date.
        var versions = await service.GetVersionsAsync(v3.Id, CancellationToken.None);
        Assert.Equal(
            new[] { thirdCreatedAt, secondCreatedAt, firstCreatedAt },
            versions.Select(version => version.CreatedAt).ToArray());
        Assert.All(versions, version => Assert.Equal(firstCreatedAt, version.RootCreatedAt));

        // Offer list: one row per offer (the latest version) carrying the root date.
        var list = await service.GetListAsync(new OfferQueryParamsDto(), CancellationToken.None);
        var row = Assert.Single(list.Items);
        Assert.Equal(v3.Id, row.Id);
        Assert.Equal(thirdCreatedAt, row.CreatedAt);
        Assert.Equal(firstCreatedAt, row.RootCreatedAt);
    }

    private static void SeedBankAccount(TestDatabase db)
    {
        db.Context.BankAccounts.Add(new BankAccount
        {
            Bank = "Test Bank",
            Branch = "Main",
            AccountHolderName = "Technocom",
            AccountNumber = "123456",
            Swift = "SWIFT",
            Iban = "EG123",
            Currency = "EGP",
            SortOrder = 1,
            IsActive = true
        });
        db.Context.SaveChanges();
    }

    // Uses the synchronous SaveChanges on purpose: it bypasses the creation-date guard of
    // SaveChangesAsync, so a test can start from an offer that "was created" in the past.
    private static DateTimeOffset SetCreatedAt(TestDatabase db, Offer offer, DateTimeOffset createdAt)
    {
        offer.CreatedAt = createdAt;
        offer.UpdatedAt = createdAt;
        db.Context.SaveChanges();
        return createdAt;
    }
}

public class CustomerOfferGroupingTests
{
    [Fact]
    public async Task Customer_offers_list_each_offer_once_with_its_latest_version()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1, serialNumber: 100);
        var latest = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 2, serialNumber: 100);
        TestFactory.AddOffer(db.Context, basics, "Loc/101/YL/AA", version: 1, serialNumber: 101);

        var service = TestFactory.CreateCustomerService(db.Context);
        var page = await service.GetOffersAsync(basics.Customer.Id, 1, 50, CancellationToken.None);

        Assert.Equal(2, page.TotalCount);
        var offer100 = Assert.Single(page.Items, item => item.Reference == "Loc/100/YL/AA");
        Assert.Equal(latest.Id, offer100.Id);
        Assert.Equal(2, offer100.Version);
        Assert.Equal(2, offer100.VersionsCount);
    }

    [Fact]
    public async Task Customer_detail_counts_and_sums_only_the_latest_version_of_each_offer()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var v1 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 1, serialNumber: 100);
        var v2 = TestFactory.AddOffer(db.Context, basics, "Loc/100/YL/AA", version: 2, serialNumber: 100);
        v1.Total = 1000m;
        v2.Total = 1500m;
        db.Context.SaveChanges();

        var service = TestFactory.CreateCustomerService(db.Context);
        var detail = await service.GetByIdWithHistoryAsync(basics.Customer.Id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(1, detail!.OffersCount);
        Assert.Equal(1500m, detail.TotalValue);
        var history = Assert.Single(detail.Offers);
        Assert.Equal(2, history.Version);
        Assert.Equal(2, history.VersionsCount);
    }
}
