using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Authorization;

/// <summary>
/// Runs after a JWT has been validated. A token stays valid for hours, but an admin must be able
/// to deactivate a user or change their role and have it apply on the very next request, so the
/// user is looked up here and the role claim is replaced with the one stored in the database.
/// </summary>
public static class ActiveUserTokenValidator
{
    public static async Task OnTokenValidated(TokenValidatedContext context)
    {
        var rawId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (context.Principal?.Identity is not ClaimsIdentity identity || !Guid.TryParse(rawId, out var userId))
        {
            context.Fail("The token has no valid user.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.Role })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (user is null || !user.IsActive)
        {
            context.Fail("The user no longer exists or is inactive.");
            return;
        }

        foreach (var claim in identity.FindAll(identity.RoleClaimType).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(identity.RoleClaimType, user.Role.ToString()));
    }
}
