using sales_portal.domain.Enums;
using sales_portal.domain.Models;

namespace sales_portal.core.Notifications;

/// <summary>
/// The single source of truth for who may receive each notification. Only active users, and
/// never the actor. Unknown event types are silent.
/// </summary>
public static class NotificationAudience
{
    private static readonly IReadOnlyDictionary<string, UserRole[]> Roles =
        new Dictionary<string, UserRole[]>(StringComparer.Ordinal)
        {
            [NotificationTypes.OfferCreated] = [UserRole.Admin, UserRole.Sales],
            [NotificationTypes.OfferRevised] = [UserRole.Admin, UserRole.Sales],
            [NotificationTypes.OfferStatusChanged] = [UserRole.Admin, UserRole.Sales, UserRole.Accountant],
            [NotificationTypes.OfferDeleted] = [UserRole.Admin, UserRole.Sales, UserRole.Accountant],
            [NotificationTypes.CustomerCreated] = [UserRole.Admin, UserRole.Sales, UserRole.Accountant],
            [NotificationTypes.CustomerDeleted] = [UserRole.Admin, UserRole.Sales, UserRole.Accountant],
            [NotificationTypes.MaterialCreated] = [UserRole.Admin, UserRole.Sales, UserRole.Accountant],
            [NotificationTypes.MaterialDeleted] = [UserRole.Admin, UserRole.Sales, UserRole.Accountant],
            [NotificationTypes.BankCreated] = [UserRole.Admin, UserRole.Sales, UserRole.Accountant],
            [NotificationTypes.BankDeleted] = [UserRole.Admin, UserRole.Sales, UserRole.Accountant],
            [NotificationTypes.ImportCommitted] = [UserRole.Admin, UserRole.Sales, UserRole.Accountant],
            [NotificationTypes.UserCreated] = [UserRole.Admin],
            [NotificationTypes.UserDeactivated] = [UserRole.Admin],
            [NotificationTypes.UserRoleChanged] = [UserRole.Admin]
        };

    public static bool Receives(string type, UserRole role) =>
        Roles.TryGetValue(type, out var allowed) && allowed.Contains(role);

    public static IReadOnlyList<User> Recipients(
        IEnumerable<User> users,
        string type,
        Guid? actorUserId) =>
        users
            .Where(user => user.IsActive
                && user.Id != actorUserId
                && Receives(type, user.Role))
            .ToList();
}
