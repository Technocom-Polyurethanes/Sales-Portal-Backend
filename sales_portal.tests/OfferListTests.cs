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
        TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 1);
        TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 2);
        TestFactory.AddOffer(db.Context, basics, "101/OCT./YL/AA", version: 1);

        var service = TestFactory.CreateOfferService(db.Context);
        var result = await service.GetListAsync(new OfferQueryParamsDto(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        var offer100 = Assert.Single(result.Items, item => item.Reference == "100/OCT./YL/AA");
        Assert.Equal(2, offer100.Version);
        Assert.Equal(2, offer100.VersionsCount);
        Assert.Equal(1, result.Items.Single(item => item.Reference == "101/OCT./YL/AA").VersionsCount);
    }

    [Fact]
    public async Task LatestOnly_false_returns_every_version()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 1);
        TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 2);

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
        var older = TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 1);
        TestFactory.AddOffer(db.Context, basics, "101/OCT./YL/AA", version: 1);

        var service = TestFactory.CreateOfferService(db.Context);
        var before = await service.GetListAsync(new OfferQueryParamsDto(), CancellationToken.None);
        Assert.Equal(2, before.Items.Count);

        await service.UpdateStatusAsync(
            older.Id,
            new UpdateOfferStatusDto { Status = OfferStatus.Issued },
            basics.SecondUser.Id,
            CancellationToken.None);

        var after = await service.GetListAsync(new OfferQueryParamsDto(), CancellationToken.None);
        Assert.Equal(older.Id, after.Items[0].Id);
        Assert.Equal(basics.SecondUser.FullName, after.Items[0].LastModifiedByName);
        Assert.True(after.Items[0].LastModifiedAt >= after.Items[1].LastModifiedAt);
    }

    [Fact]
    public async Task Status_filter_applies_to_latest_version_rows()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 1, status: OfferStatus.Issued);
        TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 2, status: OfferStatus.Draft);
        TestFactory.AddOffer(db.Context, basics, "101/OCT./YL/AA", version: 1, status: OfferStatus.Issued);

        var service = TestFactory.CreateOfferService(db.Context);

        var issued = await service.GetListAsync(
            new OfferQueryParamsDto { Status = OfferStatus.Issued },
            CancellationToken.None);

        var item = Assert.Single(issued.Items);
        Assert.Equal("101/OCT./YL/AA", item.Reference);
        Assert.Equal(1, issued.TotalCount);
    }

    [Fact]
    public async Task Paging_counts_latest_version_rows_only()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        for (var i = 0; i < 3; i++)
        {
            var reference = $"{200 + i}/OCT./YL/AA";
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
        var v1 = TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 1);
        var v2 = TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 2);

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
        TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 1);
        var v2 = TestFactory.AddOffer(
            db.Context,
            basics,
            "100/OCT./YL/AA",
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
        var v1 = TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 1, issueDate: new DateOnly(2026, 10, 15));

        var service = TestFactory.CreateOfferService(db.Context);
        var revised = await service.ReviseAsync(v1.Id, basics.User.Id, CancellationToken.None);

        Assert.NotNull(revised);
        Assert.Equal("100/OCT./YL/AA", revised.Reference);
        Assert.Equal(2, revised.Version);
        Assert.Equal(new DateOnly(2026, 10, 15), revised.IssueDate);
    }

    [Fact]
    public async Task ReviseAsync_with_different_issue_date_month_but_same_reference_fails_validation()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        // Original offer in October
        var v1 = TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 1, issueDate: new DateOnly(2026, 10, 15));

        var service = TestFactory.CreateOfferService(db.Context);

        // ReviseAsync uses the same reference and issue date from the source offer,
        // so this test verifies the validation would catch a mismatch if the issue date
        // were changed. Since ReviseAsync copies the issue date, we test the calculator directly.
        var calculator = new OfferCalculator(new OfferDocumentBuilder());
        var catalog = CreateCatalog(basics);

        // Same reference (OCT.) but issue date in November - should fail
        var draft = new OfferDraftDto
        {
            Reference = "100/OCT./YL/AA",
            IssueDate = new DateOnly(2026, 11, 5),
            CustomerId = basics.Customer.Id,
            Version = 2,
            ParentOfferId = v1.Id,
            CreatedByUserId = basics.User.Id,
            Status = OfferStatus.Draft,
            Currency = "EGP",
            PaymentTermId = basics.PaymentTerm.Id,
            DocumentLanguage = DocumentLanguage.En
        };

        var calculation = calculator.Calculate(draft, catalog);

        Assert.Contains(
            "Reference month (OCT.) does not match the issue date month (NOV.).",
            calculation.ValidationErrors);
    }

    [Fact]
    public async Task UpdateAsync_changing_issue_date_month_without_changing_reference_fails_validation()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var offer = TestFactory.AddOffer(db.Context, basics, "100/OCT./YL/AA", version: 1, issueDate: new DateOnly(2026, 10, 15));

        var service = TestFactory.CreateOfferService(db.Context);

        // Try to update the offer with a different issue date month but same reference
        var draft = new OfferDraftDto
        {
            Id = offer.Id,
            Reference = "100/OCT./YL/AA",
            Version = 1,
            CustomerId = basics.Customer.Id,
            IssueDate = new DateOnly(2026, 11, 5), // Different month!
            DueDate = new DateOnly(2026, 11, 5),
            CreatedByUserId = basics.User.Id,
            Status = OfferStatus.Draft,
            Currency = "EGP",
            PaymentTermId = basics.PaymentTerm.Id,
            DocumentLanguage = DocumentLanguage.En,
            Lines =
            [
                new OfferDraftLineDto
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 1,
                    MaterialId = Guid.NewGuid(), // Will fail validation but we only care about reference
                    RatePerKg = 100,
                    Packaging = []
                }
            ],
            DeliveryBatches = []
        };

        var exception = await Assert.ThrowsAsync<OfferValidationException>(() =>
            service.UpdateAsync(offer.Id, draft, basics.User.Id, CancellationToken.None));

        Assert.Contains(
            "Reference month (OCT.) does not match the issue date month (NOV.).",
            exception.Errors);
    }

    private static OfferCatalog CreateCatalog(SeededBasics basics)
        => new()
        {
            Customer = basics.Customer,
            PaymentTerm = basics.PaymentTerm,
            CompanySettings = new CompanySettings
            {
                Name = "TECHNOCOM",
                ReferencePrefix = "YL"
            }
        };
}
