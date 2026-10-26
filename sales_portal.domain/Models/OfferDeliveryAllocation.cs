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

        public Guid OfferLineId { get; set; }
        public OfferLine OfferLine { get; set; } = default!;

        public Guid OfferLinePackagingId { get; set; }
        public OfferLinePackaging OfferLinePackaging { get; set; } = default!;

        // Snapshot fields for fast PDF & table rendering
        public Guid MaterialId { get; set; }
        public Guid PackageTypeId { get; set; }
        public string PackageTypeName { get; set; } = default!;
        public string PackageTypeNameAr { get; set; } = default!;
        public decimal WeightPerPackageKg { get; set; }
        public int Packages { get; set; }
        public decimal TotalWeightKg { get; set; } // Packages * WeightPerPackageKg
    }
}
