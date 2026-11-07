using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

/// <summary>The username and password are not changed here; see reset-password.</summary>
public class UpdateUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string? Email { get; set; }

    /// <summary>Required. Nullable on purpose so a missing role is rejected instead of becoming Admin (0).</summary>
    public UserRole? Role { get; set; }

    public string? RoleTitle { get; set; }
    public string? RoleTitleAr { get; set; }
    public string? Team { get; set; }
    public string? TeamAr { get; set; }

    /// <summary>When omitted the current value is kept.</summary>
    public bool? IsActive { get; set; }
}
