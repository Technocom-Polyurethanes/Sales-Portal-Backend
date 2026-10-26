using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.IService;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;

namespace sales_portal.core.Services;

public class AuthService : IAuthService
{
    private readonly IRepository<User> _users;

    public AuthService(IRepository<User> users)
    {
        _users = users;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        {
            throw new ValidationException("Username and password are required.");
        }

        var user = await _users.GetAll()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == dto.Username && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid username or password.");
        }

        var secret = Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? "SuperSecretTechnocomSalesPortalKey_MustBeAtLeast32BytesLong!";
        var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "SalesPortalAPI";
        var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "SalesPortalClient";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(12);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("fullName", user.FullName),
            new Claim("initials", user.Initials)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new LoginResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt,
            User = MapUser(user)
        };
    }

    public async Task<AuthenticatedUserDto?> GetCurrentUserAsync(Guid userId)
    {
        var user = await _users.GetAll()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        return user == null ? null : MapUser(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
        {
            throw new ValidationException("The new password must be at least 8 characters long.");
        }

        var user = await _users.GetAll()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        if (user == null)
        {
            throw new NotFoundException("User was not found.");
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
        {
            throw new ValidationException("The current password is incorrect.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        _users.Update(user);
        await _users.SaveChangesAsync();
    }

    private static AuthenticatedUserDto MapUser(User user)
    {
        return new AuthenticatedUserDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Initials = user.Initials,
            Email = user.Email,
            RoleTitle = user.RoleTitle,
            RoleTitleAr = user.RoleTitleAr,
            Team = user.Team,
            TeamAr = user.TeamAr,
            Role = user.Role
        };
    }
}
