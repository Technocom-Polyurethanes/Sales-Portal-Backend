using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class OfferDeliveryAllocation : BaseEntity
    {
        public Guid DeliveryBatchId { get; set; }
        public OfferDeliveryBatch DeliveryBatch { get; set; } = default!;
        public Guid MaterialId { get; set; }
        public Material Material { get; set; } = default!;
        public int Packages { get; set; }
    }
}
