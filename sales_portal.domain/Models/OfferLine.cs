using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class OfferLine : BaseEntity
    {
        public Guid OfferId { get; set; }
        public Offer Offer { get; set; } = default!;
        public int SortOrder { get; set; }

        public Guid MaterialId { get; set; }
        public Material Material { get; set; } = default!;
        // Snapshot of the material at the time of the offer
        public string MaterialCode { get; set; } = default!;
        public string MaterialName { get; set; } = default!;
        public string Category { get; set; } = default!;

        public decimal RatePerKg { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal QuantityKg { get; set; }                    // sum of packaging rows
        public decimal Amount { get; set; }                        // qty * rate * (1 - discount/100)
        public bool ShowSpecifications { get; set; }
        public string? CustomSpecifications { get; set; }

        public ICollection<OfferLinePackaging> Packaging { get; set; } = new List<OfferLinePackaging>();
    }
}
