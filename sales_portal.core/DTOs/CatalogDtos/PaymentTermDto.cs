namespace sales_portal.core.DTOs;

public class PaymentTermDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string LabelAr { get; set; } = string.Empty;
    public int Days { get; set; }
    public bool IsActive { get; set; }
}
