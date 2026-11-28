using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sales_portal.api.Authorization;
using sales_portal.core.Audit;
using sales_portal.core.DTOs;
using sales_portal.core.IService;
using sales_portal.domain.Enums;

namespace sales_portal.api.Controllers;

/// <summary>Admin only: create users, set their role and password, deactivate them.</summary>
[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = Policies.ManageUsers)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuditContext _audit;

    public UsersController(IUserService userService, IAuditContext audit)
    {
        _userService = userService;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResultDto<UserDto>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] UserRole? role,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _userService.GetPagedAsync(
            search, role, isActive, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        return user == null ? NotFound() : Ok(user);
    }

    [Audit(AuditActions.UserCreated)]
    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(
        [FromBody] CreateUserDto request,
        CancellationToken cancellationToken)
    {
        var user = await _userService.CreateAsync(request, cancellationToken);
        _audit.SetEntity("user", user.Id, user.Username);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [Audit(AuditActions.UserUpdated)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> UpdateUser(
        Guid id,
        [FromBody] UpdateUserDto request,
        CancellationToken cancellationToken)
    {
        var actingUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            ? parsed
            : Guid.Empty;
        var user = await _userService.UpdateAsync(id, request, actingUserId, cancellationToken);
        _audit.SetEntity("user", user.Id, user.Username);
        return Ok(user);
    }

    [Audit(AuditActions.UserPasswordReset)]
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] ResetPasswordDto request,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        _audit.SetEntity("user", id, user?.Username);
        await _userService.ResetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }

    [Audit(AuditActions.UserDeleted)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        var actingUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            ? parsed
            : Guid.Empty;
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        _audit.SetEntity("user", id, user?.Username);
        return await _userService.DeleteAsync(id, actingUserId, cancellationToken)
            ? NoContent()
            : NotFound();
    }
}
