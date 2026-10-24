using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class OfferLinePackaging : BaseEntity
    {
        public Guid OfferLineId { get; set; }
        public OfferLine OfferLine { get; set; } = default!;
        public int SortOrder { get; set; }

        public Guid PackageTypeId { get; set; }
        public PackageType PackageType { get; set; } = default!;
        public string PackageTypeName { get; set; } = default!;    // snapshot
        public string PackageTypeNameAr { get; set; } = default!;  // snapshot
        public decimal WeightPerPackageKg { get; set; }
        public int Packages { get; set; }
    }
}
