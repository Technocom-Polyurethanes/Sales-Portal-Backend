using sales_portal.domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class WorkspaceSettings : BaseEntity
    {
        public bool RemindBeforeExpiry { get; set; } = true;
        public int DaysBeforeExpiry { get; set; } = 3;
        public string DefaultCurrency { get; set; } = "EGP";
        public decimal DefaultTaxPercent { get; set; } = 14;
        public Guid? DefaultPaymentTermId { get; set; }
        public PaymentTerm? DefaultPaymentTerm { get; set; }
        public DocumentLanguage DefaultDocumentLanguage { get; set; } = DocumentLanguage.En;
        public string? ErpNextUrl { get; set; }
        public string? ErpNextApiKey { get; set; }                 // store encrypted
        public string? ErpNextApiSecret { get; set; }              // store encrypted
    }
}
