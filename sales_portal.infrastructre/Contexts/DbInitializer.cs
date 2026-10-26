using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Contexts;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // Ensure database is created/migrated
        await context.Database.MigrateAsync();

        // 1. Seed Default Payment Terms
        if (!await context.PaymentTerms.AnyAsync())
        {
            context.PaymentTerms.AddRange(
                new PaymentTerm { Code = "transfer-order", Label = "Bank Transfer with Order", LabelAr = "تحويل بنكي مع امر التوريد", Days = 0 },
                new PaymentTerm { Code = "cash-delivery", Label = "Cash on Delivery", LabelAr = "نقدي عند الاستلام", Days = 0 },
                new PaymentTerm { Code = "cheque-30", Label = "Cheque Due 30 Days", LabelAr = "إصدار شيك - يستحق بعد 30 يوم", Days = 30 },
                new PaymentTerm { Code = "cheque-60", Label = "Cheque Due 60 Days", LabelAr = "إصدار شيك - يستحق بعد 60 يوم", Days = 60 }
            );
            await context.SaveChangesAsync();
        }

        var defaultTerm = await context.PaymentTerms.FirstAsync(pt => pt.Code == "transfer-order");

        // 2. Seed Admin User (Yahia Lotfy)
        if (!await context.Users.AnyAsync())
        {
            context.Users.Add(new User
            {
                Username = "Amgad",
                FullName = "Amgad Anwar",
                Initials = "AA",
                Email = "logistics@technocom.org",
                RoleTitle = "Chairman",
                RoleTitleAr = "رئيس مجلس الإدارة",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Technocom2026!"),
                Role = UserRole.Admin,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        // 3. Seed Default Customer (Duravit Egypt)
        if (!await context.Customers.AnyAsync())
        {
            context.Customers.Add(new Customer
            {
                CustomerCode = "CUST-DURAVIT",
                Name = "Duravit Egypt for Sanitary Ware",
                NameAr = "شركة ديورافيت إيجيبت لصناعة الأدوات الصحية",
                ContactPerson = "Nader William",
                City = "Badr City",
                Address = "10th of Ramadan City, Egypt",
                DefaultPaymentTermId = defaultTerm.Id,
                DefaultCurrency = "EGP"
            });
            await context.SaveChangesAsync();
        }

        // 4. Seed Package Types & Materials (Polyol & Iso)
        if (!await context.PackageTypes.AnyAsync())
        {
            context.PackageTypes.Add(new PackageType { Name = "Drum", NameAr = "برميل", IsActive = true });
            await context.SaveChangesAsync();
        }

        if (!await context.Materials.AnyAsync())
        {
            context.Materials.AddRange(
                new Material
                {
                    ItemCode = "50001",
                    Name = "DALTORIM EG 26200 - D",
                    Category = "Polyol",
                    ReferencePricePerKg = 162.00m,
                    Uom = "kg",
                    IsActive = true
                },
                new Material
                {
                    ItemCode = "30001",
                    Name = "SUPRASEC 5005T",
                    Category = "Iso",
                    ReferencePricePerKg = 162.00m,
                    Uom = "kg",
                    IsActive = true
                }
            );
            await context.SaveChangesAsync();
        }

        // 5. Seed Bank Accounts (QNB & SAIB from your email template)
        if (!await context.BankAccounts.AnyAsync())
        {
            context.BankAccounts.AddRange(
                new BankAccount
                {
                    Bank = "QNB",
                    Branch = "Badr City",
                    AccountHolderName = "شركة تكنوكوم للتوكيلات التجارية",
                    AccountNumber = "20319811443",
                    Swift = "QNBAEGCXXXX",
                    Iban = "EG100037015208182031981144393",
                    Currency = "EGP",
                    SortOrder = 1,
                    IsActive = true
                },
                new BankAccount
                {
                    Bank = "SAIB",
                    Branch = "Shubra",
                    AccountHolderName = "شركة تكنوكوم للتوكيلات التجارية",
                    AccountNumber = "30203004433-10460",
                    Swift = "SBNKEGCXXXX",
                    Iban = "EG860035003003020300443310460",
                    Currency = "EGP",
                    SortOrder = 2,
                    IsActive = true
                }
            );
            await context.SaveChangesAsync();
        }

        // 6. Seed Company Settings
        if (!await context.CompanySettings.AnyAsync())
        {
            context.CompanySettings.Add(new CompanySettings
            {
                Name = "TECHNOCOM",
                NameAr = "شركة تكنوكوم",
                Tagline = "For Commercial Agencies",
                TaglineAr = "للتوكيلات التجارية",
                FormCode = "TF-06-01A",
                ReferencePrefix = "YL",
                Address = "Badr City, Industrial Zone, Cairo, Egypt"
            });
            await context.SaveChangesAsync();
        }
    }
}