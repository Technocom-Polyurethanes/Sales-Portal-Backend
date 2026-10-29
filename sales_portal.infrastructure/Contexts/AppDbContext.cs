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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Automatically applies all IEntityTypeConfiguration<T> classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}