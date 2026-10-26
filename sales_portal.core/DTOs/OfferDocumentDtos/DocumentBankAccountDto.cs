namespace sales_portal.core.DTOs;

public class DocumentBankAccountDto
{
    public string Bank { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Swift { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string Currency { get; set; } = "EGP";
}
