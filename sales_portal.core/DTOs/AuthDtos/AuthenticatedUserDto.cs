using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class AuthenticatedUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? RoleTitle { get; set; }
    public string? RoleTitleAr { get; set; }
    public string? Team { get; set; }
    public string? TeamAr { get; set; }
    public UserRole Role { get; set; }
}
