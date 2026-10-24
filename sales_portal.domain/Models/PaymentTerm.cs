using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class PaymentTerm : BaseEntity
    {
        public string Code { get; set; } = default!;              // "cheque-30"
        public string Label { get; set; } = default!;
        public string LabelAr { get; set; } = default!;
        public int Days { get; set; }                              // 0 = due on issue
        public bool IsActive { get; set; } = true;
    }
}
