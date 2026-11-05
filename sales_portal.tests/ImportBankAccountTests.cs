using Microsoft.EntityFrameworkCore;
using sales_portal.core.Services;
using sales_portal.domain.Models;
using Xunit;

namespace SalesPortal.Tests;

public class ImportBankAccountTests
{
    [Fact]
    public async Task New_rows_get_defaults_and_are_committed()
    {
        using var db = new TestDatabase();
        db.Context.BankAccounts.Add(Existing("ACC-OLD", sortOrder: 5));
        await db.Context.SaveChangesAsync();

        var service = TestFactory.CreateImportService(db.Context);
        var preview = await PreviewAsync(
            service,
            "QNB,,Badr City,,Holder,,ACC-NEW,SWIFT,EG123,,,\r\n");

        var row = Assert.Single(preview.Rows);
        Assert.Equal("new", row.Status);
        Assert.Equal("EGP", row.Values["currency"]);
        Assert.Equal("true", row.Values["isActive"]);
        // Highest existing sort order (5) plus the row position in the file (1).
        Assert.Equal("6", row.Values["sortOrder"]);

        var result = await service.CommitAsync(preview.ImportId, CancellationToken.None);

        Assert.Equal(1, result.Created);
        var stored = await db.Context.BankAccounts.SingleAsync(account => account.AccountNumber == "ACC-NEW");
        Assert.Equal("QNB", stored.Bank);
        Assert.Equal("Badr City", stored.Branch);
        Assert.Equal("Holder", stored.AccountHolderName);
        Assert.Equal("EGP", stored.Currency);
        Assert.True(stored.IsActive);
        Assert.Equal(6, stored.SortOrder);
    }

    [Fact]
    public async Task Existing_account_is_matched_by_number_case_insensitively_and_updated()
    {
        using var db = new TestDatabase();
        db.Context.BankAccounts.Add(Existing("ACC-1", sortOrder: 1));
        await db.Context.SaveChangesAsync();

        var service = TestFactory.CreateImportService(db.Context);
        var preview = await PreviewAsync(
            service,
            "QNB,,New Branch,,Holder,,acc-1,,,,,\r\n");

        var row = Assert.Single(preview.Rows);
        Assert.Equal("update", row.Status);
        var change = Assert.Single(row.Changes);
        Assert.Equal("branch", change.Field);
        Assert.Equal("Old Branch", change.OldValue);
        Assert.Equal("New Branch", change.NewValue);

        var result = await service.CommitAsync(preview.ImportId, CancellationToken.None);

        Assert.Equal(1, result.Updated);
        var stored = await db.Context.BankAccounts.SingleAsync(account => account.AccountNumber == "ACC-1");
        Assert.Equal("New Branch", stored.Branch);
    }

    [Fact]
    public async Task Identical_rows_are_unchanged_and_skipped()
    {
        using var db = new TestDatabase();
        var existing = Existing("ACC-1", sortOrder: 1);
        existing.Swift = "SWIFT";
        existing.Iban = "EG123";
        db.Context.BankAccounts.Add(existing);
        await db.Context.SaveChangesAsync();

        var service = TestFactory.CreateImportService(db.Context);
        var preview = await PreviewAsync(
            service,
            "QNB,,Old Branch,,Holder,,ACC-1,SWIFT,EG123,EGP,true,1\r\n");

        var row = Assert.Single(preview.Rows);
        Assert.Equal("unchanged", row.Status);
        Assert.Empty(row.Changes);
        Assert.Equal("unchanged", preview.Rows[0].Status);
        Assert.Equal(1, preview.Summary.Unchanged);

        var result = await service.CommitAsync(preview.ImportId, CancellationToken.None);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public async Task Duplicate_account_number_inside_the_file_is_an_error_on_the_second_row()
    {
        using var db = new TestDatabase();
        var service = TestFactory.CreateImportService(db.Context);
        var preview = await PreviewAsync(
            service,
            "QNB,,Badr City,,Holder,,ACC-DUP,,,,,\r\n" +
            "QNB,,Badr City,,Holder,,ACC-DUP,,,,,\r\n");

        Assert.Equal("new", preview.Rows[0].Status);
        Assert.Equal("error", preview.Rows[1].Status);
        Assert.Contains(
            preview.Rows[1].Errors,
            error => error.Contains("Duplicate account number 'ACC-DUP'"));
    }

    [Fact]
    public async Task Several_existing_accounts_with_the_same_number_are_an_ambiguous_error()
    {
        using var db = new TestDatabase();
        db.Context.BankAccounts.AddRange(
            Existing("ACC-AMB", sortOrder: 1),
            Existing("acc-amb", sortOrder: 2));
        await db.Context.SaveChangesAsync();

        var service = TestFactory.CreateImportService(db.Context);
        var preview = await PreviewAsync(
            service,
            "QNB,,Badr City,,Holder,,ACC-AMB,,,,,\r\n");

        var row = Assert.Single(preview.Rows);
        Assert.Equal("error", row.Status);
        Assert.Contains(row.Errors, error => error.Contains("ambiguous"));
        Assert.Null(row.Existing);

        var result = await service.CommitAsync(preview.ImportId, CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(2, await db.Context.BankAccounts.CountAsync());
    }

    [Fact]
    public async Task Missing_required_bank_branch_and_account_number_are_all_reported()
    {
        using var db = new TestDatabase();
        var service = TestFactory.CreateImportService(db.Context);
        var preview = await PreviewAsync(
            service,
            ",,,,\"Holder\",,,SWIFT,,,,\r\n");

        var row = Assert.Single(preview.Rows);
        Assert.Equal("error", row.Status);
        Assert.Contains(row.Errors, error => error.Contains("Bank is required"));
        Assert.Contains(row.Errors, error => error.Contains("Branch is required"));
        Assert.Contains(row.Errors, error => error.Contains("Account number is required"));
    }

    [Fact]
    public async Task Template_has_the_bank_account_columns()
    {
        using var db = new TestDatabase();
        var service = TestFactory.CreateImportService(db.Context);

        var template = await service.GetTemplateAsync("bank-accounts", CancellationToken.None);
        var text = System.Text.Encoding.UTF8.GetString(template.Content);

        Assert.Equal("bank-accounts-template.csv", template.FileName);
        Assert.Equal(ImportTestFiles.BankAccountHeader, text.Split("\r\n")[0].TrimStart('\uFEFF'));
    }

    private static BankAccount Existing(string accountNumber, int sortOrder) => new()
    {
        Bank = "QNB",
        Branch = "Old Branch",
        AccountHolderName = "Holder",
        AccountNumber = accountNumber,
        Swift = "SWIFT",
        Iban = "EG123",
        Currency = "EGP",
        SortOrder = sortOrder,
        IsActive = true
    };

    private static Task<sales_portal.core.DTOs.ImportPreviewDto> PreviewAsync(
        ImportService service,
        string rows)
    {
        var stream = ImportTestFiles.Csv(ImportTestFiles.BankAccountHeader + "\r\n" + rows);
        return service.PreviewAsync(
            "bank-accounts", stream, "bank-accounts.csv", stream.Length, null, CancellationToken.None);
    }
}
