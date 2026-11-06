using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using Xunit;

namespace SalesPortal.Tests;

public class OfferBankSelectionTests
{
    [Fact]
    public async Task Null_selection_stores_all_active_accounts_and_the_document_lists_them_in_sort_order()
    {
        using var db = new TestDatabase();
        var setup = Seed(db);

        var detail = await CreateAsync(db, setup, bankAccountIds: null);

        Assert.Equal(new[] { setup.BankB.Id, setup.BankA.Id }, detail.BankAccountIds);
        Assert.Equal(
            new[] { "Bank B", "Bank A" },
            detail.Document!.BankAccounts.Select(account => account.Bank));
    }

    [Fact]
    public async Task Subset_selection_stores_only_the_selected_accounts_and_filters_the_document()
    {
        using var db = new TestDatabase();
        var setup = Seed(db);

        var detail = await CreateAsync(db, setup, bankAccountIds: [setup.BankA.Id]);

        Assert.Equal(new[] { setup.BankA.Id }, detail.BankAccountIds);
        var bank = Assert.Single(detail.Document!.BankAccounts);
        Assert.Equal("Bank A", bank.Bank);
    }

    [Fact]
    public async Task Empty_selection_is_rejected()
    {
        using var db = new TestDatabase();
        var setup = Seed(db);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateAsync(db, setup, bankAccountIds: []));

        Assert.Contains("Select at least one bank account", exception.Message);
    }

    [Fact]
    public async Task Unknown_or_inactive_ids_are_rejected()
    {
        using var db = new TestDatabase();
        var setup = Seed(db);

        var unknown = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateAsync(db, setup, bankAccountIds: [Guid.NewGuid()]));
        Assert.Contains("not found or is inactive", unknown.Message);

        var inactive = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateAsync(db, setup, bankAccountIds: [setup.Inactive.Id]));
        Assert.Contains("not found or is inactive", inactive.Message);
    }

    [Fact]
    public async Task Revise_copies_the_selection_from_the_source_version()
    {
        using var db = new TestDatabase();
        var setup = Seed(db);
        var source = await CreateAsync(db, setup, bankAccountIds: [setup.BankA.Id]);

        var service = TestFactory.CreateOfferService(db.Context);
        var revised = await service.ReviseAsync(source.Id, setup.Basics.User.Id, CancellationToken.None);

        Assert.Equal(2, revised.Version);
        Assert.Equal(new[] { setup.BankA.Id }, revised.BankAccountIds);
        var bank = Assert.Single(revised.Document!.BankAccounts);
        Assert.Equal("Bank A", bank.Bank);
    }

    [Fact]
    public async Task Legacy_offer_without_a_selection_returns_null()
    {
        using var db = new TestDatabase();
        var setup = Seed(db);
        var offer = TestFactory.AddOffer(
            db.Context,
            setup.Basics,
            "Loc/100/YL/AA",
            issueDate: new DateOnly(2026, 10, 21));

        var service = TestFactory.CreateOfferService(db.Context);
        var detail = await service.GetByIdAsync(offer.Id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Null(detail!.BankAccountIds);
    }

    private static async Task<OfferDetailDto> CreateAsync(
        TestDatabase db,
        Setup setup,
        Guid[]? bankAccountIds)
    {
        var draft = new OfferDraftDto
        {
            Reference = "Loc/100/YL/AA",
            OfferType = OfferType.Loc,
            IssueDate = new DateOnly(2026, 10, 21),
            CustomerId = setup.Basics.Customer.Id,
            PaymentTermId = setup.Basics.PaymentTerm.Id,
            BankAccountIds = bankAccountIds,
            Lines =
            [
                new OfferDraftLineDto
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 1,
                    MaterialId = setup.Material.Id,
                    RatePerKg = 10m,
                    Packaging =
                    [
                        new OfferDraftPackagingDto
                        {
                            Id = Guid.NewGuid(),
                            SortOrder = 1,
                            PackageTypeId = setup.Drum.Id,
                            WeightPerPackageKg = 200m,
                            Packages = 1
                        }
                    ]
                }
            ]
        };

        var service = TestFactory.CreateOfferService(db.Context);
        return await service.CreateAsync(draft, setup.Basics.User.Id, CancellationToken.None);
    }

    private static Setup Seed(TestDatabase db)
    {
        var basics = TestFactory.SeedBasics(db.Context);
        var material = new Material { ItemCode = "MAT-1", Name = "Material", Category = "Polyol" };
        var drum = new PackageType { Name = "Drum", NameAr = "برميل" };
        var bankA = Bank("Bank A", sortOrder: 2);
        var bankB = Bank("Bank B", sortOrder: 1);
        var inactive = Bank("Bank Inactive", sortOrder: 0);
        inactive.IsActive = false;

        db.Context.AddRange(material, drum, bankA, bankB, inactive);
        db.Context.SaveChanges();

        return new Setup(basics, material, drum, bankA, bankB, inactive);
    }

    private static BankAccount Bank(string name, int sortOrder) => new()
    {
        Bank = name,
        Branch = "Branch",
        AccountHolderName = "Holder",
        AccountNumber = name.Replace(" ", string.Empty),
        Swift = "SWIFT",
        Iban = "EG123",
        Currency = "EGP",
        SortOrder = sortOrder,
        IsActive = true
    };

    private sealed record Setup(
        SeededBasics Basics,
        Material Material,
        PackageType Drum,
        BankAccount BankA,
        BankAccount BankB,
        BankAccount Inactive);
}
