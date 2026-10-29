namespace sales_portal.core.DTOs;

public class CustomerLookupItemDto
{
    public Guid Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPersonAr { get; set; }
    public string? City { get; set; }
    public string? CityAr { get; set; }
    public string? TaxId { get; set; }
    public string DefaultCurrency { get; set; } = "EGP";
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
