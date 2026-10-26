using Microsoft.EntityFrameworkCore;
using sales_portal.domain.Models;

namespace sales_portal.infrastructure.Contexts;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PackageType> PackageTypes => Set<PackageType>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<PaymentTerm> PaymentTerms => Set<PaymentTerm>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<WorkspaceSettings> WorkspaceSettings => Set<WorkspaceSettings>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<OfferLine> OfferLines => Set<OfferLine>();
    public DbSet<OfferLinePackaging> OfferLinePackagings => Set<OfferLinePackaging>();
    public DbSet<OfferDeliveryBatch> OfferDeliveryBatches => Set<OfferDeliveryBatch>();
    public DbSet<OfferDeliveryAllocation> OfferDeliveryAllocations => Set<OfferDeliveryAllocation>();
    public DbSet<OfferPaymentMilestone> OfferPaymentMilestones => Set<OfferPaymentMilestone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Automatically applies all IEntityTypeConfiguration<T> classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}