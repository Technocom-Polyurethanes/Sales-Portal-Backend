namespace sales_portal.core.DTOs;

public class DocumentBillToDto
{
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? ContactPerson { get; set; }
    public string? Attention { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
