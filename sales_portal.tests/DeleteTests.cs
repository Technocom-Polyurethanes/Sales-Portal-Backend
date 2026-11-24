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
    public async Task Deleting_catalogue_records_hides_them_without_removing_the_rows()
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

        Assert.True(await new CustomerService(new Repository<Customer>(db.Context))
            .DeleteAsync(basics.Customer.Id, CancellationToken.None));
        Assert.True(await new MaterialService(new Repository<Material>(db.Context))
            .DeleteAsync(material.Id, CancellationToken.None));
        Assert.True(await new BankAccountService(new Repository<BankAccount>(db.Context))
            .DeleteAsync(bank.Id, CancellationToken.None));

        Assert.Empty(db.Context.Customers);
        Assert.Empty(db.Context.Materials);
        Assert.Empty(db.Context.BankAccounts);
        Assert.True(db.Context.Customers.IgnoreQueryFilters().Single().IsDeleted);
        Assert.True(db.Context.Materials.IgnoreQueryFilters().Single().IsDeleted);
        Assert.True(db.Context.BankAccounts.IgnoreQueryFilters().Single().IsDeleted);
    }

    [Fact]
    public async Task Deleting_an_offer_hides_it_but_keeps_its_sequence_number()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var offer = TestFactory.AddOffer(db.Context, basics, "Loc/0001-2026/YL/TU");
        var service = TestFactory.CreateOfferService(db.Context);

        Assert.True(await service.DeleteAsync(offer.Id, CancellationToken.None));
        Assert.Empty(db.Context.Offers);
        Assert.Equal(1, db.Context.Offers.IgnoreQueryFilters().Count());

        var next = await service.GetNextSequenceAsync(
            OfferType.Loc, basics.User.Id, CancellationToken.None);
        Assert.Equal(2, next.SuggestedSequence);
    }
}
