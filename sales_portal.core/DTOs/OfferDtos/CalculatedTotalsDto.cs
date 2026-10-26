using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class CalculatedTotalsDto
{
    public decimal Subtotal { get; set; }
    public decimal OrderDiscount { get; set; }
    public decimal OrderDiscountPercent { get; set; }
    public decimal NetAfterDiscount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public bool OtherTaxEnabled { get; set; }
    public string OtherTaxName { get; set; } = string.Empty;
    public decimal OtherTaxPercent { get; set; }
    public decimal OtherTaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string Currency { get; set; } = "EGP";
    public DocumentLanguage DocumentLanguage { get; set; }
}
