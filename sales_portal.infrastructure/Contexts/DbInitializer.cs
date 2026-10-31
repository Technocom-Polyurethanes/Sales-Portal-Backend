using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Contexts;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context, bool seedDemoUser)
    {
        // 1. Seed Default Payment Terms
        if (!await context.PaymentTerms.AnyAsync())
        {
            context.PaymentTerms.AddRange(
                new PaymentTerm { Code = "transfer-order", Label = "Bank Transfer with Order", LabelAr = "تحويل بنكي مع امر التوريد", Days = 0 },
                new PaymentTerm { Code = "cash-delivery", Label = "Cash on Delivery", LabelAr = "نقدي عند الاستلام", Days = 0 },
                new PaymentTerm { Code = "cheque-30", Label = "Cheque Due 30 Days", LabelAr = "إصدار شيك يستحق بعد 30 يوم", Days = 30 },
                new PaymentTerm { Code = "cheque-60", Label = "Cheque Due 60 Days", LabelAr = "إصدار شيك يستحق بعد 60 يوم", Days = 60 }
            );
            await context.SaveChangesAsync();
        }

        var defaultTerm = await context.PaymentTerms.FirstAsync(pt => pt.Code == "transfer-order");

        // 2. Seed Admin User (development only)
        if (seedDemoUser && !await context.Users.AnyAsync())
        {
            context.Users.Add(new User
            {
                Username = "Amgad",
                FullName = "Amgad Anwar",
                Initials = "AA",
                Email = "logistics@technocom.org",
                RoleTitle = "Sales",
                RoleTitleAr = "مبيعات",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Technocom2026!"),
                Role = UserRole.Admin,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        // 3. Bootstrap Admin User (production first-login)
        await BootstrapAdminAsync(context);

        // 4. Seed Package Types
        var packageTypeSeeds = new[]
        {
            new PackageType { Name = "Drum", NameAr = "برميل", IsActive = true },
            new PackageType { Name = "Jerrycan", NameAr = "جركن", IsActive = true },
            new PackageType { Name = "Pail", NameAr = "بستله", IsActive = true },
            new PackageType { Name = "IBC Tank", NameAr = "تانك IBC", IsActive = true }
        };

        foreach (var packageType in packageTypeSeeds)
        {
            if (!await context.PackageTypes.AnyAsync(pt => pt.Name == packageType.Name))
            {
                context.PackageTypes.Add(packageType);
            }
        }

        await context.SaveChangesAsync();

        var drumPackageType = await context.PackageTypes.FirstAsync(pt => pt.Name == "Drum");

        // 5. Seed Default Customer (Duravit Egypt)
        var duravit = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-DURAVIT");
        if (duravit == null)
        {
            duravit = new Customer
            {
                CustomerCode = "CUST-DURAVIT",
                Name = "Duravit Egypt for Sanitary Ware",
                NameAr = "شركة ديورافيت إيجيبت لصناعة الأدوات الصحية",
                TaxId = "200-145-890",
                ContactPerson = "Nader William",
                ContactPersonAr = "نادر وليام",
                City = "Badr City",
                CityAr = "مدينة بدر",
                Address = "10th of Ramadan City, Egypt",
                AddressAr = "العاشر من رمضان، مصر",
                DefaultPaymentTermId = defaultTerm.Id,
                DefaultCurrency = "EGP"
            };
            context.Customers.Add(duravit);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(duravit.TaxId))
            {
                duravit.TaxId = "200-145-890";
            }

            duravit.DefaultPaymentTermId ??= defaultTerm.Id;
            duravit.ContactPersonAr ??= "نادر وليام";
            duravit.CityAr ??= "مدينة بدر";
            duravit.AddressAr ??= "العاشر من رمضان، مصر";
        }

        await context.SaveChangesAsync();

        // 6. Seed Materials (Polyol & Iso) with default packaging information
        var materialSeeds = new[]
        {
            new Material
            {
                ItemCode = "50001",
                Name = "DALTORIM EG 26200 - D",
                Category = "Polyol",
                ReferencePricePerKg = 162.00m,
                Uom = "kg",
                IsActive = true,
                DefaultPackageTypeId = drumPackageType.Id,
                DefaultWeightKg = 210m
            },
            new Material
            {
                ItemCode = "30001",
                Name = "SUPRASEC 5005T",
                Category = "Iso",
                ReferencePricePerKg = 162.00m,
                Uom = "kg",
                IsActive = true,
                DefaultPackageTypeId = drumPackageType.Id,
                DefaultWeightKg = 250m
            }
        };

        foreach (var materialSeed in materialSeeds)
        {
            if (!await context.Materials.AnyAsync(m => m.ItemCode == materialSeed.ItemCode))
            {
                context.Materials.Add(materialSeed);
            }
        }

        await context.SaveChangesAsync();

        // Keep existing seeded material rows aligned with the current defaults.
        // Only fill missing values so later edits made in the UI are not overwritten.
        var daltorim = await context.Materials.FirstOrDefaultAsync(m => m.ItemCode == "50001");
        if (daltorim != null)
        {
            daltorim.DefaultPackageTypeId ??= drumPackageType.Id;
            if (daltorim.DefaultWeightKg <= 0m)
            {
                daltorim.DefaultWeightKg = 210m;
            }
        }

        var suprasec = await context.Materials.FirstOrDefaultAsync(m => m.ItemCode == "30001");
        if (suprasec != null)
        {
            suprasec.DefaultPackageTypeId ??= drumPackageType.Id;
            if (suprasec.DefaultWeightKg <= 0m)
            {
                suprasec.DefaultWeightKg = 250m;
            }
        }

        await context.SaveChangesAsync();

        // 7. Seed Bank Accounts (QNB & SAIB)
        var bankSeeds = new[]
        {
            new BankAccount
            {
                Bank = "QNB",
                BankAr = "بنك قطر الوطني",
                Branch = "Badr City",
                BranchAr = "مدينة بدر",
                AccountHolderName = "Technocom for Commercial Agencies",
                AccountHolderNameAr = "شركة تكنوكوم للتوكيلات التجارية",
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
                BankAr = "البنك العربي الدولي (SAIB)",
                Branch = "Shubra",
                BranchAr = "شبرا",
                AccountHolderName = "Technocom for Commercial Agencies",
                AccountHolderNameAr = "شركة تكنوكوم للتوكيلات التجارية",
                AccountNumber = "30203004433-10460",
                Swift = "SBNKEGCXXXX",
                Iban = "EG860035003003020300443310460",
                Currency = "EGP",
                SortOrder = 2,
                IsActive = true
            }
        };

        foreach (var bankSeed in bankSeeds)
        {
            var existingBank = await context.BankAccounts
                .FirstOrDefaultAsync(account => account.Bank == bankSeed.Bank);

            if (existingBank == null)
            {
                context.BankAccounts.Add(bankSeed);
                continue;
            }

            // Only fill missing Arabic values so edits made in settings are preserved.
            existingBank.BankAr ??= bankSeed.BankAr;
            existingBank.BranchAr ??= bankSeed.BranchAr;
            existingBank.AccountHolderNameAr ??= bankSeed.AccountHolderNameAr;
        }

        await context.SaveChangesAsync();

        // 8. Seed Company Settings
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

    /// <summary>
    /// Creates an initial admin user when the Users table is empty and all required
    /// bootstrap environment variables are provided. This is intended for the very first
    /// production deployment so that someone can log in and start using the system.
    /// </summary>
    public static async Task BootstrapAdminAsync(AppDbContext context)
    {
        // Only run when no users exist at all
        if (await context.Users.AnyAsync())
        {
            return;
        }

        var username = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_USERNAME");
        var password = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD");
        var fullName = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_FULL_NAME");
        var initials = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_INITIALS");
        var email = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_EMAIL");

        // All four required variables must be present
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(initials))
        {
            return; // Silently skip bootstrap when variables are not set
        }

        // Enforce minimum password length
        if (password.Length < 12)
        {
            throw new InvalidOperationException(
                "BOOTSTRAP_ADMIN_PASSWORD must be at least 12 characters long.");
        }

        context.Users.Add(new User
        {
            Username = username.Trim(),
            FullName = fullName.Trim(),
            Initials = initials.Trim().ToUpperInvariant(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Role = UserRole.Admin,
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        });

        await context.SaveChangesAsync();
    }
}