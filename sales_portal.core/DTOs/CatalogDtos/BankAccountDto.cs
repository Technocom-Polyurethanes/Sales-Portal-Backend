namespace sales_portal.core.DTOs;

public class BankAccountDto
{
    public Guid Id { get; set; }
    public string Bank { get; set; } = string.Empty;
    public string? BankAr { get; set; }
    public string Branch { get; set; } = string.Empty;
    public string? BranchAr { get; set; }
    public string AccountHolderName { get; set; } = string.Empty;
    public string? AccountHolderNameAr { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Swift { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string Currency { get; set; } = "EGP";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
