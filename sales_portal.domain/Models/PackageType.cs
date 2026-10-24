using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class PackageType : BaseEntity
    {
        public string Name { get; set; } = default!;              // e.g., "Drum"
        public string NameAr { get; set; } = default!;          // e.g., "برميل"
        public bool IsActive { get; set; } = true;
    }
}
