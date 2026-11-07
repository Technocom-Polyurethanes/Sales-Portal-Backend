using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Enums
{
    /// <summary>
    /// Stored as an integer. Sales keeps the value 1 that SalesUser had, so existing rows need no migration.
    /// </summary>
    public enum UserRole { Admin = 0, Sales = 1, Accountant = 2 }

}
