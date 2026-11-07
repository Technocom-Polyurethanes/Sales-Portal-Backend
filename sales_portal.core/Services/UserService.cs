using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.IService;
using sales_portal.domain.Enums;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;

namespace sales_portal.core.Services;

/// <summary>Admin-side user management: create, edit, deactivate and reset passwords.</summary>
public class UserService : IUserService
{
    public const int MinPasswordLength = 8;

    // BCrypt only uses the first 72 bytes, so a longer password would silently be cut.
    public const int MaxPasswordBytes = 72;

    private static readonly Regex UsernameRegex = new(
        @"^[A-Za-z0-9._-]{3,50}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // The initials become the last segment of the offer reference (see ReferenceRules).
    private static readonly Regex InitialsRegex = new(
        @"^[A-Za-z0-9]{1,6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IRepository<User> _users;

    public UserService(IRepository<User> users)
    {
        _users = users;
    }

    public async Task<PaginatedResultDto<UserDto>> GetPagedAsync(
        string? search,
        UserRole? role,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _users.GetAll().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(term) ||
                u.FullName.ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResultDto<UserDto>
        {
            Items = rows.Select(Map).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _users.GetAll().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user == null ? null : Map(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken)
    {
        var errors = ValidateProfile(dto.FullName, dto.Initials, dto.Email, dto.Role,
            dto.RoleTitle, dto.RoleTitleAr, dto.Team, dto.TeamAr);

        var username = (dto.Username ?? string.Empty).Trim();
        if (!UsernameRegex.IsMatch(username))
        {
            errors["username"] = ["Username must be 3-50 characters: letters, digits, dot, dash or underscore."];
        }
        else
        {
            var lowered = username.ToLower();
            var taken = await _users.GetAll()
                .AnyAsync(u => u.Username.ToLower() == lowered, cancellationToken);
            if (taken)
            {
                errors["username"] = ["This username is already taken."];
            }
        }

        var passwordError = ValidatePassword(dto.Password);
        if (passwordError != null)
        {
            errors["password"] = [passwordError];
        }

        ThrowIfInvalid(errors);

        var user = new User
        {
            Username = username,
            FullName = dto.FullName.Trim(),
            Initials = dto.Initials.Trim().ToUpperInvariant(),
            Email = NullIfBlank(dto.Email),
            Role = dto.Role!.Value,
            RoleTitle = NullIfBlank(dto.RoleTitle),
            RoleTitleAr = NullIfBlank(dto.RoleTitleAr),
            Team = NullIfBlank(dto.Team),
            TeamAr = NullIfBlank(dto.TeamAr),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            IsActive = dto.IsActive
        };

        await _users.AddAsync(user);
        await _users.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task<UserDto> UpdateAsync(
        Guid id,
        UpdateUserDto dto,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetAll().FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var errors = ValidateProfile(dto.FullName, dto.Initials, dto.Email, dto.Role,
            dto.RoleTitle, dto.RoleTitleAr, dto.Team, dto.TeamAr);
        ThrowIfInvalid(errors);

        var newRole = dto.Role!.Value;
        var newIsActive = dto.IsActive ?? user.IsActive;

        // Never let the system end up without an active administrator.
        var losesAdminAccess = user.Role == UserRole.Admin && user.IsActive
            && (newRole != UserRole.Admin || !newIsActive);
        if (losesAdminAccess)
        {
            if (id == actingUserId)
            {
                throw new ConflictException("You cannot change your own role or deactivate your own account.");
            }

            var otherActiveAdmins = await _users.GetAll()
                .CountAsync(u => u.Id != id && u.Role == UserRole.Admin && u.IsActive, cancellationToken);
            if (otherActiveAdmins == 0)
            {
                throw new ConflictException("At least one active administrator is required.");
            }
        }

        user.FullName = dto.FullName.Trim();
        user.Initials = dto.Initials.Trim().ToUpperInvariant();
        user.Email = NullIfBlank(dto.Email);
        user.Role = newRole;
        user.RoleTitle = NullIfBlank(dto.RoleTitle);
        user.RoleTitleAr = NullIfBlank(dto.RoleTitleAr);
        user.Team = NullIfBlank(dto.Team);
        user.TeamAr = NullIfBlank(dto.TeamAr);
        user.IsActive = newIsActive;

        await _users.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordDto dto, CancellationToken cancellationToken)
    {
        var user = await _users.GetAll().FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var passwordError = ValidatePassword(dto.NewPassword);
        if (passwordError != null)
        {
            ThrowIfInvalid(new Dictionary<string, string[]> { ["newPassword"] = [passwordError] });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _users.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, string[]> ValidateProfile(
        string? fullName,
        string? initials,
        string? email,
        UserRole? role,
        string? roleTitle,
        string? roleTitleAr,
        string? team,
        string? teamAr)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            errors["fullName"] = ["Full name is required."];
        }
        else if (fullName.Trim().Length > 100)
        {
            errors["fullName"] = ["Full name must be 100 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(initials) || !InitialsRegex.IsMatch(initials.Trim()))
        {
            errors["initials"] = ["Initials are required: 1-6 letters or digits, printed in offer references."];
        }

        if (!string.IsNullOrWhiteSpace(email) && (email.Trim().Length > 150 || !EmailRegex.IsMatch(email.Trim())))
        {
            errors["email"] = ["Enter a valid email address."];
        }

        if (role == null || !Enum.IsDefined(typeof(UserRole), role.Value))
        {
            errors["role"] = ["Choose a role."];
        }

        foreach (var (key, value) in new[]
        {
            ("roleTitle", roleTitle), ("roleTitleAr", roleTitleAr), ("team", team), ("teamAr", teamAr)
        })
        {
            if (value != null && value.Trim().Length > 100)
            {
                errors[key] = ["Must be 100 characters or fewer."];
            }
        }

        return errors;
    }

    private static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            return $"Password must be at least {MinPasswordLength} characters.";
        }

        if (Encoding.UTF8.GetByteCount(password) > MaxPasswordBytes)
        {
            return $"Password must be at most {MaxPasswordBytes} bytes long.";
        }

        return null;
    }

    private static void ThrowIfInvalid(IDictionary<string, string[]> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        // The flat message is what a toast shows; the field map lets the form mark each input.
        var message = string.Join(" ", errors.Values.SelectMany(messages => messages));
        throw new ValidationException(message, errors);
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static UserDto Map(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        FullName = user.FullName,
        Initials = user.Initials,
        Email = user.Email,
        Role = user.Role,
        RoleTitle = user.RoleTitle,
        RoleTitleAr = user.RoleTitleAr,
        Team = user.Team,
        TeamAr = user.TeamAr,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt
    };
}
