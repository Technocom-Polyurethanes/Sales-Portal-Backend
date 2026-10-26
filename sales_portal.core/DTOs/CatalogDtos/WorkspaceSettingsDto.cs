using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class WorkspaceSettingsDto
{
    public Guid Id { get; set; }
    public bool RemindBeforeExpiry { get; set; }
    public int DaysBeforeExpiry { get; set; }
    public string DefaultCurrency { get; set; } = "EGP";
    public decimal DefaultTaxPercent { get; set; }
    public Guid? DefaultPaymentTermId { get; set; }
    public DocumentLanguage DefaultDocumentLanguage { get; set; }
    public string? ErpNextUrl { get; set; }
}
