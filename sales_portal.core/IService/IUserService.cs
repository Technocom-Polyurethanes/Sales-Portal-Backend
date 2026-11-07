using sales_portal.core.DTOs;
using sales_portal.domain.Enums;

namespace sales_portal.core.IService;

public interface IUserService
{
    Task<PaginatedResultDto<UserDto>> GetPagedAsync(
        string? search,
        UserRole? role,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken);

    /// <summary><paramref name="actingUserId"/> is the admin making the change, used to block self lock-out.</summary>
    Task<UserDto> UpdateAsync(Guid id, UpdateUserDto dto, Guid actingUserId, CancellationToken cancellationToken);

    Task ResetPasswordAsync(Guid id, ResetPasswordDto dto, CancellationToken cancellationToken);
}
