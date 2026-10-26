using sales_portal.core.DTOs;

namespace sales_portal.core.IService;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto);
    Task<AuthenticatedUserDto?> GetCurrentUserAsync(Guid userId);
    Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
}
