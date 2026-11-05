using sales_portal.core.Exceptions;

namespace sales_portal.core.Services.Import;

internal enum ImportKind
{
    Customers,
    Materials,
    Prices,
    BankAccounts
}

internal static class ImportKinds
{
    public static ImportKind Parse(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "customers" => ImportKind.Customers,
            "materials" => ImportKind.Materials,
            "prices" => ImportKind.Prices,
            "bank-accounts" => ImportKind.BankAccounts,
            _ => throw new ValidationException(
                "Unknown import type. Use customers, materials, prices or bank-accounts.")
        };
    }

    public static string ToSlug(this ImportKind kind) => kind switch
    {
        ImportKind.Customers => "customers",
        ImportKind.Materials => "materials",
        ImportKind.BankAccounts => "bank-accounts",
        _ => "prices"
    };
}
