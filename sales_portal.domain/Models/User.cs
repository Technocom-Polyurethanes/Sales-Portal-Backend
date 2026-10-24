using sales_portal.domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class User : BaseEntity
    {
        public string Username { get; set; } = default!;          // unique
        public string FullName { get; set; } = default!;
        public string Initials { get; set; } = default!;          // used in the reference (YL, AA)
        public string? Email { get; set; }
        public string? RoleTitle { get; set; }                     // "Sales manager"
        public string? RoleTitleAr { get; set; }
        public string? Team { get; set; }                          // "Sales team"
        public string? TeamAr { get; set; }
        public string PasswordHash { get; set; } = default!;      // BCrypt
        public UserRole Role { get; set; } = UserRole.SalesUser;
        public bool IsActive { get; set; } = true;

        public ICollection<Offer> Offers { get; set; } = new List<Offer>();
    }
}
