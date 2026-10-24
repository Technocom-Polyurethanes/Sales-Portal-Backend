using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class CompanySettings : BaseEntity
    {
        public string Name { get; set; } = default!;
        public string NameAr { get; set; } = default!;
        public string Tagline { get; set; } = default!;
        public string TaglineAr { get; set; } = default!;
        public string FormCode { get; set; } = "TF-06-01";
        public string ReferencePrefix { get; set; } = "YL";
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? LogoUrl { get; set; }
        public decimal LogoWidthMm { get; set; } = 55;
        public decimal LogoHeightMm { get; set; } = 25;
    }
}
