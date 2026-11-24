using Microsoft.EntityFrameworkCore;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using sales_portal.infrastructure;
using Xunit;

namespace SalesPortal.Tests;

public class DeleteTests
{
    [Fact]
    public async Task Deleting_catalogue_records_removes_the_rows()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var material = new Material { ItemCode = "MAT-1", Name = "Polyol", Category = "Polyol" };
        var bank = new BankAccount
        {
            Bank = "Test Bank",
            Branch = "Cairo",
            AccountHolderName = "Test Holder",
            AccountNumber = "123",
            Swift = "TESTEGCA",
            Iban = "EG000000000000000000000000000"
        };
        db.Context.AddRange(material, bank);
        db.Context.SaveChanges();

        Assert.True(await new CustomerService(
            new Repository<Customer>(db.Context),
            new Repository<Offer>(db.Context)).DeleteAsync(basics.Customer.Id, CancellationToken.None));
        Assert.True(await new MaterialService(
            new Repository<Material>(db.Context),
            new Repository<Offer>(db.Context)).DeleteAsync(material.Id, CancellationToken.None));
        Assert.True(await new BankAccountService(new Repository<BankAccount>(db.Context))
            .DeleteAsync(bank.Id, CancellationToken.None));

        Assert.Empty(db.Context.Customers);
        Assert.Empty(db.Context.Materials);
        Assert.Empty(db.Context.BankAccounts);
    }

    [Fact]
    public async Task Deleting_an_offer_removes_every_version_of_the_chain()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        const string reference = "Loc/0001-2026/YL/TU";
        var firstVersion = TestFactory.AddOffer(db.Context, basics, reference, version: 1);
        var latestVersion = TestFactory.AddOffer(db.Context, basics, reference, version: 2);
        var otherOffer = TestFactory.AddOffer(db.Context, basics, "Loc/0002-2026/YL/TU");
        var service = TestFactory.CreateOfferService(db.Context);

        Assert.True(await service.DeleteAsync(latestVersion.Id, CancellationToken.None));

        Assert.Equal(otherOffer.Id, db.Context.Offers.Single().Id);
        Assert.DoesNotContain(db.Context.Offers, o => o.Id == firstVersion.Id);
        Assert.DoesNotContain(db.Context.Offers, o => o.Id == latestVersion.Id);
    }

    [Fact]
    public async Task Deleting_a_customer_removes_their_offers()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var offer = TestFactory.AddOffer(db.Context, basics, "Loc/0001-2026/YL/TU");

        Assert.True(await new CustomerService(
            new Repository<Customer>(db.Context),
            new Repository<Offer>(db.Context)).DeleteAsync(
                basics.Customer.Id, CancellationToken.None));

        Assert.Empty(db.Context.Customers);
        Assert.Empty(db.Context.Offers);
    }

    [Fact]
    public async Task Deleting_a_material_removes_the_offers_that_used_it()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var material = new Material { ItemCode = "MAT-1", Name = "Polyol", Category = "Polyol" };
        db.Context.Materials.Add(material);
        db.Context.SaveChanges();
        var offer = TestFactory.AddOffer(db.Context, basics, "Loc/0001-2026/YL/TU");
        db.Context.OfferLines.Add(new OfferLine
        {
            OfferId = offer.Id,
            MaterialId = material.Id,
            MaterialCode = material.ItemCode,
            MaterialName = material.Name,
            Category = material.Category,
            SortOrder = 1
        });
        db.Context.SaveChanges();

        Assert.True(await new MaterialService(
            new Repository<Material>(db.Context),
            new Repository<Offer>(db.Context)).DeleteAsync(material.Id, CancellationToken.None));

        Assert.Empty(db.Context.Materials);
        Assert.Empty(db.Context.Offers);
    }

    [Fact]
    public async Task Deleting_a_user_removes_the_offers_they_created()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var offer = TestFactory.AddOffer(db.Context, basics, "Loc/0001-2026/YL/TU");

        Assert.True(await new UserService(
            new Repository<User>(db.Context),
            new Repository<Offer>(db.Context)).DeleteAsync(
                basics.User.Id, basics.SecondUser.Id, CancellationToken.None));

        Assert.DoesNotContain(db.Context.Users, u => u.Id == basics.User.Id);
        Assert.Empty(db.Context.Offers);
    }
}
