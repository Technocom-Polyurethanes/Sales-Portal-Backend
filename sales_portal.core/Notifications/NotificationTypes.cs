namespace sales_portal.core.Notifications;

/// <summary>The event names shared by the backend and the frontend.</summary>
public static class NotificationTypes
{
    public const string OfferCreated = "offer.created";
    public const string OfferRevised = "offer.revised";
    public const string OfferStatusChanged = "offer.status_changed";
    public const string OfferDeleted = "offer.deleted";
    public const string CustomerCreated = "customer.created";
    public const string CustomerDeleted = "customer.deleted";
    public const string MaterialCreated = "material.created";
    public const string MaterialDeleted = "material.deleted";
    public const string BankCreated = "bank.created";
    public const string BankDeleted = "bank.deleted";
    public const string ImportCommitted = "import.committed";
    public const string UserCreated = "user.created";
    public const string UserDeactivated = "user.deactivated";
    public const string UserRoleChanged = "user.role_changed";
}
