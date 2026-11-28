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
    public const string Exported = "audit.exported";

    public static readonly string[] Categories =
    [
        "auth", "offers", "customers", "materials", "banks", "catalog", "settings", "users", "imports", "audit"
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
        ImportCommitted, Exported
    ];

    /// <summary>Explicit fallback text per action; service-supplied descriptions always win.</summary>
    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Login] = "Signed in",
        [LoginFailed] = "Failed sign-in attempt",
        [PasswordChanged] = "Changed own password",
        [OfferCreated] = "Created offer",
        [OfferUpdated] = "Updated offer",
        [OfferRevised] = "Revised offer",
        [OfferStatusChanged] = "Changed offer status",
        [OfferDeleted] = "Deleted offer",
        [OfferDocumentOpened] = "Opened the proforma document",
        [CustomerCreated] = "Created customer",
        [CustomerUpdated] = "Updated customer",
        [CustomerDeleted] = "Deleted customer",
        [MaterialCreated] = "Created material",
        [MaterialUpdated] = "Updated material",
        [MaterialDeleted] = "Deleted material",
        [BankCreated] = "Created bank account",
        [BankUpdated] = "Updated bank account",
        [BankDeleted] = "Deleted bank account",
        [PaymentTermCreated] = "Created payment term",
        [PaymentTermUpdated] = "Updated payment term",
        [SettingsUpdated] = "Updated settings",
        [UserCreated] = "Created user",
        [UserUpdated] = "Updated user",
        [UserPasswordReset] = "Reset user password",
        [UserDeleted] = "Deleted user",
        [ImportCommitted] = "Committed import",
        [Exported] = "Exported audit log"
    };

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
            "audit" => "audit",
            _ => prefix
        };
    }

    public static string DescriptionFor(string action) =>
        Descriptions.TryGetValue(action, out var description) ? description : string.Empty;
}
