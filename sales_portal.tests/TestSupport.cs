using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using sales_portal.infrastructure;
using sales_portal.infrastructure.Contexts;

namespace SalesPortal.Tests;

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
        Guid? updatedByUserId = null)
    {
        var offer = new Offer
        {
            Reference = reference,
            SerialNumber = version,
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
