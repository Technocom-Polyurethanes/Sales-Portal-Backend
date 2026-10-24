using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class Material : BaseEntity
    {
        public string ItemCode { get; set; } = default!;          // unique
        public string? ErpNextItemCode { get; set; }
        public string Name { get; set; } = default!;
        public string Category { get; set; } = default!;          // Polyol | Iso | System | Additive
        public decimal? ReferencePricePerKg { get; set; }
        public string Uom { get; set; } = "kg";
        public bool IsActive { get; set; } = true;
    }
}
