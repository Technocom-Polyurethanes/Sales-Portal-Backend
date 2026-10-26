using sales_portal.domain.Enums;

namespace sales_portal.core.DTOs;

public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
}

public class UserDto
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
