namespace sales_portal.core.Audit;

/// <summary>The action codes written to the audit log and their display categories.</summary>
public static class AuditActions
{
    public const string Login = "auth.login";
    public const string LoginFailed = "auth.login_failed";
    public const string PasswordChanged = "auth.password_changed";
    public const string OfferCreated = "offer.created";
    public const string OfferUpdated = "offer.updated";
    public const string OfferRevised = "offer.revised";
    public const string OfferStatusChanged = "offer.status_changed";
    public const string OfferDeleted = "offer.deleted";
    public const string OfferDocumentOpened = "offer.document_opened";
    public const string CustomerCreated = "customer.created";
    public const string CustomerUpdated = "customer.updated";
    public const string CustomerDeleted = "customer.deleted";
    public const string MaterialCreated = "material.created";
    public const string MaterialUpdated = "material.updated";
    public const string MaterialDeleted = "material.deleted";
    public const string BankCreated = "bank.created";
    public const string BankUpdated = "bank.updated";
    public const string BankDeleted = "bank.deleted";
    public const string PaymentTermCreated = "catalog.payment_term_created";
    public const string PaymentTermUpdated = "catalog.payment_term_updated";
    public const string SettingsUpdated = "settings.updated";
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserPasswordReset = "user.password_reset";
    public const string UserDeleted = "user.deleted";
    public const string ImportCommitted = "import.committed";

    public static readonly string[] Categories =
    [
        "auth", "offers", "customers", "materials", "banks", "catalog", "settings", "users", "imports"
    ];

    public static readonly string[] All =
    [
        Login, LoginFailed, PasswordChanged,
        OfferCreated, OfferUpdated, OfferRevised, OfferStatusChanged, OfferDeleted, OfferDocumentOpened,
        CustomerCreated, CustomerUpdated, CustomerDeleted,
        MaterialCreated, MaterialUpdated, MaterialDeleted,
        BankCreated, BankUpdated, BankDeleted,
        PaymentTermCreated, PaymentTermUpdated,
        SettingsUpdated,
        UserCreated, UserUpdated, UserPasswordReset, UserDeleted,
        ImportCommitted
    ];

    public static string CategoryFor(string action)
    {
        var prefix = action.Split('.')[0];
        return prefix switch
        {
            "auth" => "auth",
            "offer" => "offers",
            "customer" => "customers",
            "material" => "materials",
            "bank" => "banks",
            "catalog" => "catalog",
            "settings" => "settings",
            "user" => "users",
            "import" => "imports",
            _ => prefix
        };
    }
}
