namespace sales_portal.core.DTOs;

/// <summary>A payment term created or edited by the user, with its English and Arabic wording.</summary>
public class UpsertPaymentTermDto
{
    public string Label { get; set; } = string.Empty;
    public string LabelAr { get; set; } = string.Empty;
    public int Days { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Conditions { get; set; }
    public string? ConditionsAr { get; set; }
}
