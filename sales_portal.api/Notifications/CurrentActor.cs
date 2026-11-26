using System.Security.Claims;
using sales_portal.core.Notifications;

namespace sales_portal.api.Notifications;

/// <summary>Reads the signed-in user from the current HTTP request, if there is one.</summary>
public sealed class CurrentActor : ICurrentActor
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentActor(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;

            var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? DisplayName
    {
        get
        {
            var user = _accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;

            return user.FindFirstValue("fullName")
                ?? user.FindFirstValue(ClaimTypes.Name);
        }
    }
}
