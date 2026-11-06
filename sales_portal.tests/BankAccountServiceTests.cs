using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.infrastructure;
using sales_portal.domain.Models;
using Xunit;

namespace SalesPortal.Tests;

public class BankAccountServiceTests
{
    private static BankAccount NewAccount(
        string bank,
        string branch = "Cairo",
        string currency = "EGP",
        string accountNumber = "1000",
        int sortOrder = 1,
        bool isActive = true) => new()
    {
        Bank = bank,
        Branch = branch,
        AccountNumber = accountNumber,
        AccountHolderName = "Technocom",
        Swift = "SWIFT",
        Iban = "IBAN",
        Currency = currency,
        SortOrder = sortOrder,
        IsActive = isActive
    };

    [Fact]
    public async Task List_pages_and_filters_by_search_currency_and_status()
    {
        using var db = new TestDatabase();
        db.Context.BankAccounts.AddRange(
            NewAccount("QNB", "Badr City", "EGP", "20319811443", 1),
            NewAccount("SAIB", "Shubra", "EGP", "30203004433", 2),
            NewAccount("HSBC", "Dubai", "USD", "7788", 3, isActive: false));
        await db.Context.SaveChangesAsync();
        var service = TestFactory.CreateBankAccountService(db.Context);

        var firstPage = await service.GetPagedAsync(null, null, null, 1, 2, CancellationToken.None);
        var byBank = await service.GetPagedAsync("qnb", null, null, 1, 20, CancellationToken.None);
        var byBranch = await service.GetPagedAsync("shubra", null, null, 1, 20, CancellationToken.None);
        var byCurrency = await service.GetPagedAsync(null, "usd", null, 1, 20, CancellationToken.None);
        var active = await service.GetPagedAsync(null, null, true, 1, 20, CancellationToken.None);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(new[] { "QNB", "SAIB" }, firstPage.Items.Select(account => account.Bank));
        Assert.Equal("QNB", Assert.Single(byBank.Items).Bank);
        Assert.Equal("SAIB", Assert.Single(byBranch.Items).Bank);
        Assert.Equal("HSBC", Assert.Single(byCurrency.Items).Bank);
        Assert.Equal(new[] { "QNB", "SAIB" }, active.Items.Select(account => account.Bank));
    }

    [Fact]
    public async Task Create_assigns_the_next_sort_order_and_returns_the_stored_account()
    {
        using var db = new TestDatabase();
        db.Context.BankAccounts.Add(NewAccount("QNB", sortOrder: 4));
        await db.Context.SaveChangesAsync();
        var service = TestFactory.CreateBankAccountService(db.Context);

        var created = await service.CreateAsync(
            new UpsertBankAccountDto
            {
                Bank = " SAIB ",
                Branch = " Shubra ",
                AccountNumber = " 30203004433 ",
                Currency = ""
            },
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("SAIB", created.Bank);
        Assert.Equal("Shubra", created.Branch);
        Assert.Equal("30203004433", created.AccountNumber);
        Assert.Equal("EGP", created.Currency);
        Assert.Equal(5, created.SortOrder);
        Assert.True(created.IsActive);
        Assert.NotNull(await service.GetByIdAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Update_changes_the_account_and_keeps_a_missing_id_not_found()
    {
        using var db = new TestDatabase();
        var account = NewAccount("QNB");
        db.Context.BankAccounts.Add(account);
        await db.Context.SaveChangesAsync();
        var service = TestFactory.CreateBankAccountService(db.Context);

        var updated = await service.UpdateAsync(
            account.Id,
            new UpsertBankAccountDto
            {
                Bank = "QNB",
                Branch = "Alexandria",
                AccountNumber = "999",
                Swift = "QNBAEGCXXXX",
                Iban = "EG100",
                Currency = "USD",
                SortOrder = 7,
                IsActive = false
            },
            CancellationToken.None);
        var missing = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpsertBankAccountDto { Bank = "QNB", Branch = "Cairo", AccountNumber = "1" },
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Alexandria", updated!.Branch);
        Assert.Equal("QNBAEGCXXXX", updated.Swift);
        Assert.Equal("EG100", updated.Iban);
        Assert.Equal("USD", updated.Currency);
        Assert.Equal(7, updated.SortOrder);
        Assert.False(updated.IsActive);
        Assert.Null(missing);
    }

    [Fact]
    public async Task Required_fields_are_validated()
    {
        using var db = new TestDatabase();
        var service = TestFactory.CreateBankAccountService(db.Context);

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            new UpsertBankAccountDto { Bank = " ", Branch = "", AccountNumber = "" },
            CancellationToken.None));

        Assert.NotNull(error.FieldErrors);
        Assert.Contains("bank", error.FieldErrors!.Keys);
        Assert.Contains("branch", error.FieldErrors.Keys);
        Assert.Contains("accountNumber", error.FieldErrors.Keys);
    }
}
