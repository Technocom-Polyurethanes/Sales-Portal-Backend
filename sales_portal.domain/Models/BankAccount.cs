using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class BankAccount : BaseEntity
    {
        public string Bank { get; set; } = default!;
        public string Branch { get; set; } = default!;
        public string AccountHolderName { get; set; } = default!;
        public string AccountNumber { get; set; } = default!;
        public string Swift { get; set; } = default!;
        public string Iban { get; set; } = default!;
        public string Currency { get; set; } = "EGP";
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;                 // all active banks print on every offer
    }
}
