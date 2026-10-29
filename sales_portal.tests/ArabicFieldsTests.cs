using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using Xunit;

namespace SalesPortal.Tests;

public class ArabicFieldsTests
{
    [Fact]
    public void Arabic_document_uses_Arabic_bill_to_and_bank_values()
    {
        var draft = new OfferDraftDto
        {
            Reference = "100/OCT./YL/AA",
            CustomerId = Guid.NewGuid(),
            Attention = "English attention",
            AttentionAr = "انتباه عربي",
            IssueDate = new DateOnly(2026, 10, 1),
            DocumentLanguage = DocumentLanguage.Ar
        };

        var catalog = BuildCatalog();
        var calculation = BuildCalculation(draft);

        var document = new OfferDocumentBuilder().Build(draft, catalog, calculation);

        Assert.Equal("Test Customer", document.BillTo.Name);
        Assert.Equal("عميل اختبار", document.BillTo.NameAr);
        Assert.Equal("Test address", document.BillTo.Address);
        Assert.Equal("عنوان عربي", document.BillTo.AddressAr);
        Assert.Equal("Cairo", document.BillTo.City);
        Assert.Equal("القاهرة", document.BillTo.CityAr);
        Assert.Equal("English attention", document.BillTo.Attention);
        Assert.Equal("انتباه عربي", document.BillTo.AttentionAr);
        Assert.Equal("بنك قطر الوطني", document.BankAccounts[0].BankAr);
        Assert.Equal("مدينة بدر", document.BankAccounts[0].BranchAr);
        Assert.Equal("شركة تكنوكوم", document.BankAccounts[0].AccountHolderNameAr);
    }

    [Fact]
    public void Arabic_document_falls_back_to_English_when_Arabic_values_are_empty()
    {
        var draft = new OfferDraftDto
        {
            Reference = "100/OCT./YL/AA",
            CustomerId = Guid.NewGuid(),
            Attention = "Fallback attention",
            IssueDate = new DateOnly(2026, 10, 1),
            DocumentLanguage = DocumentLanguage.Ar
        };

        var catalog = BuildCatalog();
        catalog.Customer!.NameAr = null;
        catalog.Customer.AddressAr = null;
        catalog.Customer.CityAr = null;
        catalog.Customer.ContactPersonAr = null;
        catalog.BankAccounts[0].BankAr = null;
        catalog.BankAccounts[0].BranchAr = null;
        catalog.BankAccounts[0].AccountHolderNameAr = null;

        var calculation = BuildCalculation(draft);
        var document = new OfferDocumentBuilder().Build(draft, catalog, calculation);

        Assert.Equal("Test Customer", document.BillTo.NameAr);
        Assert.Equal("Test address", document.BillTo.AddressAr);
        Assert.Equal("Cairo", document.BillTo.CityAr);
        Assert.Equal("English contact", document.BillTo.ContactPersonAr);
        Assert.Equal("Fallback attention", document.BillTo.AttentionAr);
        Assert.Equal("QNB", document.BankAccounts[0].BankAr);
        Assert.Equal("Badr City", document.BankAccounts[0].BranchAr);
        Assert.Equal("English holder", document.BankAccounts[0].AccountHolderNameAr);
    }

    [Fact]
    public void English_document_keeps_all_English_values_unchanged()
    {
        var draft = new OfferDraftDto
        {
            Reference = "100/OCT./YL/AA",
            CustomerId = Guid.NewGuid(),
            Attention = "English attention",
            IssueDate = new DateOnly(2026, 10, 1),
            DocumentLanguage = DocumentLanguage.En
        };

        var catalog = BuildCatalog();
        var calculation = BuildCalculation(draft);
        var document = new OfferDocumentBuilder().Build(draft, catalog, calculation);

        Assert.Equal("Test Customer", document.BillTo.Name);
        Assert.Equal("Test address", document.BillTo.Address);
        Assert.Equal("Cairo", document.BillTo.City);
        Assert.Equal("English contact", document.BillTo.ContactPerson);
        Assert.Equal("English attention", document.BillTo.Attention);
        Assert.Equal("QNB", document.BankAccounts[0].Bank);
        Assert.Equal("Badr City", document.BankAccounts[0].Branch);
        Assert.Equal("English holder", document.BankAccounts[0].AccountHolderName);
    }

    [Fact]
    public async Task Customer_create_and_update_round_trip_Arabic_fields()
    {
        using var db = new TestDatabase();
        var service = TestFactory.CreateCustomerService(db.Context);

        var created = await service.CreateAsync(
            new UpsertCustomerDto
            {
                CustomerCode = "CUST-AR",
                Name = "Arabic Customer",
                NameAr = "عميل عربي",
                Address = "English address",
                AddressAr = "عنوان عربي",
                City = "Cairo",
                CityAr = "القاهرة",
                ContactPerson = "English contact",
                ContactPersonAr = "مسؤول عربي"
            },
            CancellationToken.None);

        Assert.Equal("عنوان عربي", created.AddressAr);
        Assert.Equal("القاهرة", created.CityAr);
        Assert.Equal("مسؤول عربي", created.ContactPersonAr);
        Assert.Equal("عميل عربي", created.NameAr);

        var updated = await service.UpdateAsync(
            created.Id,
            new UpsertCustomerDto
            {
                CustomerCode = "CUST-AR",
                Name = "Arabic Customer",
                NameAr = "عميل عربي محدث",
                Address = "English address",
                AddressAr = "عنوان محدث",
                City = "Alexandria",
                CityAr = "الإسكندرية",
                ContactPerson = "English contact",
                ContactPersonAr = "مسؤول محدث"
            },
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("عميل عربي محدث", updated.NameAr);
        Assert.Equal("عنوان محدث", updated.AddressAr);
        Assert.Equal("الإسكندرية", updated.CityAr);
        Assert.Equal("مسؤول محدث", updated.ContactPersonAr);
    }

    [Fact]
    public async Task Settings_round_trip_bank_Arabic_fields()
    {
        using var db = new TestDatabase();
        var service = TestFactory.CreateSettingsService(db.Context);
        var id = Guid.NewGuid();

        var updated = await service.UpdateAsync(
            new UpdateSettingsDto
            {
                Workspace = new WorkspaceSettingsDto
                {
                    BankAccounts =
                    [
                        new BankAccountDto
                        {
                            Id = id,
                            Bank = "QNB",
                            BankAr = "بنك قطر الوطني",
                            Branch = "Badr City",
                            BranchAr = "مدينة بدر",
                            AccountHolderName = "Technocom for Commercial Agencies",
                            AccountHolderNameAr = "شركة تكنوكوم للتوكيلات التجارية",
                            AccountNumber = "123",
                            Swift = "SWIFT",
                            Iban = "EG",
                            Currency = "EGP",
                            SortOrder = 1,
                            IsActive = true
                        }
                    ]
                }
            },
            CancellationToken.None);

        var bank = Assert.Single(updated.Workspace.BankAccounts);
        Assert.Equal(id, bank.Id);
        Assert.Equal("بنك قطر الوطني", bank.BankAr);
        Assert.Equal("مدينة بدر", bank.BranchAr);
        Assert.Equal("شركة تكنوكوم للتوكيلات التجارية", bank.AccountHolderNameAr);

        var reloaded = await service.GetAsync(CancellationToken.None);
        var reloadedBank = Assert.Single(reloaded.Workspace.BankAccounts);
        Assert.Equal("بنك قطر الوطني", reloadedBank.BankAr);
    }

    [Fact]
    public async Task Settings_rejects_bank_accounts_without_bank_branch_or_account_number()
    {
        using var db = new TestDatabase();
        var service = TestFactory.CreateSettingsService(db.Context);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateAsync(
                new UpdateSettingsDto
                {
                    Workspace = new WorkspaceSettingsDto
                    {
                        BankAccounts =
                        [
                            new BankAccountDto
                            {
                                Bank = "QNB",
                                Branch = " ",
                                AccountNumber = string.Empty
                            }
                        ]
                    }
                },
                CancellationToken.None));

        Assert.NotNull(exception.FieldErrors);
        Assert.Contains("workspace.bankAccounts[0].branch", exception.FieldErrors!.Keys);
        Assert.Contains("workspace.bankAccounts[0].accountNumber", exception.FieldErrors.Keys);
    }

    private static OfferCatalog BuildCatalog()
    {
        return new OfferCatalog
        {
            Customer = new Customer
            {
                CustomerCode = "CUST-TEST",
                Name = "Test Customer",
                NameAr = "عميل اختبار",
                Address = "Test address",
                AddressAr = "عنوان عربي",
                City = "Cairo",
                CityAr = "القاهرة",
                ContactPerson = "English contact",
                ContactPersonAr = "مسؤول عربي"
            },
            PaymentTerm = new PaymentTerm
            {
                Code = "transfer-order",
                Label = "Bank Transfer",
                LabelAr = "تحويل بنكي",
                Days = 0
            },
            BankAccounts =
            [
                new BankAccount
                {
                    Bank = "QNB",
                    BankAr = "بنك قطر الوطني",
                    Branch = "Badr City",
                    BranchAr = "مدينة بدر",
                    AccountHolderName = "English holder",
                    AccountHolderNameAr = "شركة تكنوكوم",
                    AccountNumber = "123",
                    Swift = "SWIFT",
                    Iban = "EG",
                    SortOrder = 1,
                    IsActive = true
                }
            ],
            CompanySettings = new CompanySettings
            {
                Name = "TECHNOCOM",
                NameAr = "شركة تكنوكوم",
                Tagline = "For Commercial Agencies",
                TaglineAr = "للتوكيلات التجارية",
                FormCode = "TF-06-01A",
                ReferencePrefix = "YL"
            }
        };
    }

    private static OfferCalculateResponseDto BuildCalculation(OfferDraftDto draft)
    {
        return new OfferCalculateResponseDto
        {
            Draft = draft,
            Totals = new CalculatedTotalsDto
            {
                IssueDate = draft.IssueDate,
                DueDate = draft.IssueDate,
                Currency = draft.Currency,
                DocumentLanguage = draft.DocumentLanguage
            }
        };
    }
}
