using sales_portal.domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class Offer : BaseEntity
    {
        public string Reference { get; set; } = default!;          // Loc/42/YL/JD or Int/43/YL/JD
        public OfferType OfferType { get; set; } = OfferType.Loc;
        public int SerialNumber { get; set; }
        public int Version { get; set; } = 1;
        /// <summary>Stable id shared by every version of an offer, so the reference may change between versions.</summary>
        public Guid RootOfferId { get; set; }
        public Guid? ParentOfferId { get; set; }
        public Offer? ParentOffer { get; set; }

        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; } = default!;
        public Guid CreatedByUserId { get; set; }
        public User CreatedBy { get; set; } = default!;
        public Guid? UpdatedByUserId { get; set; }
        public User? UpdatedBy { get; set; }

        public string? Attention { get; set; }                     // editable contact person printed on the PDF
        public string? AttentionAr { get; set; }
        public string? TaxId { get; set; }                         // printed in the PDF Bill-To block
        public int ValidityDays { get; set; }                      // days the offer stays valid after the issue date, 0 prints no validity row
        public DateOnly IssueDate { get; set; }
        public DateOnly DueDate { get; set; }
        public Guid PaymentTermId { get; set; }
        public PaymentTerm PaymentTerm { get; set; } = default!;
        public string Currency { get; set; } = "EGP";
        public DocumentLanguage DocumentLanguage { get; set; } = DocumentLanguage.En;
        public string? Notes { get; set; }
        public OfferDeliveryMode DeliveryMode { get; set; } = OfferDeliveryMode.Batches;
        public string? DeliveryText { get; set; }                  // free wording, used only when DeliveryMode is Text
        public string? DeliveryPlace { get; set; }                 // where the goods are handed over, printed as its own block
        public string? PaymentTermsText { get; set; }              // extra payment conditions (2-4 lines), printed in the payment terms block
        public string FormCode { get; set; } = "TF-06-01A";
        public OfferStatus Status { get; set; } = OfferStatus.Draft;

        /// <summary>
        /// Selected bank accounts, as a PostgreSQL uuid[]. No foreign key on purpose: deleting
        /// an account in Settings must not fail because an offer used it. Legacy offers are null.
        /// </summary>
        public Guid[]? BankAccountIds { get; set; }

        // Money calculations (no extra expenses, advance payment, or balance due)
        public decimal Subtotal { get; set; }
        public decimal OrderDiscount { get; set; }
        public decimal OrderDiscountPercent { get; set; }
        public decimal TaxPercent { get; set; }
        public decimal TaxAmount { get; set; }
        public bool OtherTaxEnabled { get; set; }
        public string OtherTaxName { get; set; } = "ضريبة أرباح تجارية و صناعية (1%)";
        public decimal OtherTaxPercent { get; set; } = 1.0m;         // fixed 1% deduction
        public decimal OtherTaxAmount { get; set; }                 // subtracted from total
        public decimal Total { get; set; }                          // final bottom row total

        public string SnapshotJson { get; set; } = "{}";            // jsonb, immutable copy of the printed document

        public ICollection<OfferLine> Lines { get; set; } = new List<OfferLine>();
        public ICollection<OfferDeliveryBatch> DeliveryBatches { get; set; } = new List<OfferDeliveryBatch>();
    }
}
