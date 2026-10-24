using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class OfferDeliveryBatch : BaseEntity
    {
        public Guid OfferId { get; set; }
        public Offer Offer { get; set; } = default!;
        public int BatchNumber { get; set; }
        public DateOnly DeliveryDate { get; set; }

        public ICollection<OfferDeliveryAllocation> Allocations { get; set; } = new List<OfferDeliveryAllocation>();
    }
}
