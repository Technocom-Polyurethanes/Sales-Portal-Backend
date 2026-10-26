using sales_portal.domain.Models;

namespace sales_portal.core.Services;

public class OfferCatalog
{
    public Customer? Customer { get; set; }
    public PaymentTerm? PaymentTerm { get; set; }
    public IReadOnlyList<PaymentTerm> PaymentTerms { get; set; } = Array.Empty<PaymentTerm>();
    public IReadOnlyList<Material> Materials { get; set; } = Array.Empty<Material>();
    public IReadOnlyList<PackageType> PackageTypes { get; set; } = Array.Empty<PackageType>();
    public IReadOnlyList<BankAccount> BankAccounts { get; set; } = Array.Empty<BankAccount>();
    public CompanySettings? CompanySettings { get; set; }
    public WorkspaceSettings? WorkspaceSettings { get; set; }
    public User? CreatedBy { get; set; }
}
