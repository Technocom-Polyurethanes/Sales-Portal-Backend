using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class OfferPaymentMilestone : BaseEntity
    {
        public Guid OfferId { get; set; }
        public Offer Offer { get; set; } = default!;
        public string PhaseDescription { get; set; } = default!;
        public decimal PercentageOrFixed { get; set; }
        public decimal AmountDue { get; set; }
        public string? DueDateOrEvent { get; set; }
    }
}
