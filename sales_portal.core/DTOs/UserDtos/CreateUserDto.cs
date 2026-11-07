using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class CreateUserDto
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    /// <summary>Printed in the offer reference (for example JD), letters and digits only.</summary>
    public string Initials { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>Required. Nullable on purpose so a missing role is rejected instead of becoming Admin (0).</summary>
    public UserRole? Role { get; set; }

    public string? RoleTitle { get; set; }
    public string? RoleTitleAr { get; set; }
    public string? Team { get; set; }
    public string? TeamAr { get; set; }
    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
