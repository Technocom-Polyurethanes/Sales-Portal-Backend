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

    /// <summary>
    /// Bank accounts managed from settings. The list is returned on GET and reconciled
    /// (upsert by id) on PUT when supplied. A null value means the request did not
    /// include bank accounts, so the existing list is left untouched.
    /// </summary>
    public List<BankAccountDto>? BankAccounts { get; set; }
}
