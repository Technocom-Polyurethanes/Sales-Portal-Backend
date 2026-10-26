namespace sales_portal.core.DTOs;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public AuthenticatedUserDto User { get; set; } = new();
}
