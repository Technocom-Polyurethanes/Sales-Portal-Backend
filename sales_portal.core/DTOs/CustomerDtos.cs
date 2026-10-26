using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class CustomerLookupDto
{
    public Guid Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? ContactPerson { get; set; }
    public string? City { get; set; }
    public string? TaxId { get; set; }
    public string DefaultCurrency { get; set; } = "EGP";
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class CustomerDto : CustomerLookupDto
{
    public string? ErpNextId { get; set; }
    public string Address { get; set; } = string.Empty;
    public Guid? DefaultPaymentTermId { get; set; }
    public string? DefaultPaymentTermLabel { get; set; }
    public int OffersCount { get; set; }
    public decimal TotalValue { get; set; }
    public DateOnly? LastOfferDate { get; set; }
}

public class CustomerDetailsDto : CustomerDto
{
    public List<CustomerOfferHistoryDto> Offers { get; set; } = new();
}

public class CustomerOfferHistoryDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateOnly IssueDate { get; set; }
    public OfferStatus Status { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "EGP";
}

public class CustomerUpsertDto
{
    public string CustomerCode { get; set; } = string.Empty;
    public string? ErpNextId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? TaxId { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string Address { get; set; } = string.Empty;
    public Guid? DefaultPaymentTermId { get; set; }
    public string DefaultCurrency { get; set; } = "EGP";
}
