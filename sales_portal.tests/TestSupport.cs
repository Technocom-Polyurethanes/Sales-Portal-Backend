using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using sales_portal.core.Caching;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using sales_portal.infrastructure;
using sales_portal.infrastructure.Contexts;

namespace SalesPortal.Tests;

// Tests that read or set process-wide environment variables (JWT_*, BOOTSTRAP_ADMIN_*,
// host startup) share this collection so xUnit runs them one at a time.
internal static class ProcessEnvironment
{
    public const string Name = "ProcessEnvironment";
}

internal sealed class TestDatabase : IDisposable
{
    public TestDatabase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"sales-portal-tests-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Context = new AppDbContext(options);
    }

    public AppDbContext Context { get; }

    public void Dispose() => Context.Dispose();
}

internal static class TestFactory
{
    public static OfferService CreateOfferService(AppDbContext context)
    {
        var catalog = new CatalogService(
            new Repository<PaymentTerm>(context),
            new Repository<PackageType>(context),
            new Repository<BankAccount>(context),
            new Repository<CompanySettings>(context),
            new Repository<WorkspaceSettings>(context),
            new Repository<Customer>(context),
            new Repository<User>(context),
            new Repository<Material>(context));

        return new OfferService(
            new Repository<Offer>(context),
            new Repository<OfferLine>(context),
            new Repository<OfferLinePackaging>(context),
            new Repository<OfferDeliveryBatch>(context),
            new Repository<OfferDeliveryAllocation>(context),
            catalog,
            new OfferCalculator(new OfferDocumentBuilder()));
    }

    public static CustomerService CreateCustomerService(AppDbContext context)
        => new(new Repository<Customer>(context));

    public static BankAccountService CreateBankAccountService(
        AppDbContext context,
        IReferenceDataCache? cache = null)
        => new(new Repository<BankAccount>(context), cache);

    public static ImportService CreateImportService(AppDbContext context)
        => new(
            new Repository<ImportBatch>(context),
            new Repository<Customer>(context),
            new Repository<Material>(context),
            new Repository<PaymentTerm>(context),
            new Repository<User>(context),
            new Repository<BankAccount>(context));

    public static SettingsService CreateSettingsService(AppDbContext context)
        => new(
            new Repository<CompanySettings>(context),
            new Repository<WorkspaceSettings>(context),
            new Repository<BankAccount>(context));

    public static SeededBasics SeedBasics(AppDbContext context)
    {
        var user = new User
        {
            Username = "tester",
            FullName = "Test User",
            Initials = "TU",
            PasswordHash = "not-used",
            Role = UserRole.Admin
        };

        var secondUser = new User
        {
            Username = "modifier",
            FullName = "Modifier User",
            Initials = "MU",
            PasswordHash = "not-used",
            Role = UserRole.Admin
        };

        var customer = new Customer
        {
            CustomerCode = "CUST-TEST",
            Name = "Test Customer",
            NameAr = "عميل اختبار",
            Address = "Test address",
            City = "Cairo"
        };

        var paymentTerm = new PaymentTerm
        {
            Code = "transfer-order",
            Label = "Bank Transfer",
            LabelAr = "تحويل بنكي",
            Days = 0
        };

        context.AddRange(user, secondUser, customer, paymentTerm);
        context.SaveChanges();

        return new SeededBasics(user, secondUser, customer, paymentTerm);
    }

    public static Offer AddOffer(
        AppDbContext context,
        SeededBasics basics,
        string reference,
        int version = 1,
        OfferStatus status = OfferStatus.Draft,
        DateOnly? issueDate = null,
        Guid? updatedByUserId = null,
        int? serialNumber = null,
        OfferType offerType = OfferType.Loc)
    {
        var offer = new Offer
        {
            Reference = reference,
            OfferType = offerType,
            SerialNumber = serialNumber ?? version,
            Version = version,
            CustomerId = basics.Customer.Id,
            CreatedByUserId = basics.User.Id,
            UpdatedByUserId = updatedByUserId ?? basics.User.Id,
            IssueDate = issueDate ?? new DateOnly(2026, 10, 1),
            DueDate = issueDate ?? new DateOnly(2026, 10, 1),
            PaymentTermId = basics.PaymentTerm.Id,
            Status = status,
            SnapshotJson = "{}"
        };

        // Versions that share a reference form one chain, like offers created through the service.
        offer.RootOfferId = context.Offers
            .Where(existing => existing.Reference == reference)
            .Select(existing => (Guid?)existing.RootOfferId)
            .FirstOrDefault() ?? offer.Id;

        context.Offers.Add(offer);
        context.SaveChanges();
        return offer;
    }
}

internal sealed record SeededBasics(
    User User,
    User SecondUser,
    Customer Customer,
    PaymentTerm PaymentTerm);

internal static class ImportTestFiles
{
    public static MemoryStream Csv(string text) => new(System.Text.Encoding.UTF8.GetBytes(text));

    public static MemoryStream BomCsv(string text) => new(
    [
        0xEF, 0xBB, 0xBF,
        .. System.Text.Encoding.UTF8.GetBytes(text)
    ]);

    public const string CustomerHeader =
        "customerCode,erpNextId,name,nameAr,taxId,contactPerson,contactPersonAr,email,phone,city,cityAr,address,addressAr,defaultPaymentTerm,defaultCurrency";

    public const string MaterialHeader =
        "itemCode,erpNextItemCode,name,category,referencePricePerKg,uom,isActive,defaultWeightKg";

    public const string PriceHeader = "itemCode,referencePricePerKg";

    public const string BankAccountHeader =
        "bank,bankAr,branch,branchAr,accountHolderName,accountHolderNameAr,accountNumber,swift,iban,currency,isActive,sortOrder";

    public static Task<sales_portal.core.DTOs.ImportPreviewDto> PreviewAsync(
        AppDbContext context,
        string type,
        string csv,
        string fileName = "import.csv")
    {
        var stream = Csv(csv);
        return TestFactory.CreateImportService(context)
            .PreviewAsync(type, stream, fileName, stream.Length, null, CancellationToken.None);
    }
}
