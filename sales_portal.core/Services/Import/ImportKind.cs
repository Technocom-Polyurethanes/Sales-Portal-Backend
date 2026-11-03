using sales_portal.core.Exceptions;

namespace sales_portal.core.Services.Import;

internal enum ImportKind
{
    Customers,
    Materials,
    Prices
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
            _ => throw new ValidationException(
                "Unknown import type. Use customers, materials or prices.")
        };
    }

    public static string ToSlug(this ImportKind kind) => kind switch
    {
        ImportKind.Customers => "customers",
        ImportKind.Materials => "materials",
        _ => "prices"
    };
}
