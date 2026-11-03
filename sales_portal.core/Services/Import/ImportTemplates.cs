namespace sales_portal.core.Services.Import;

/// <summary>
/// The template columns of every import type. These names are part of the API
/// contract: the preview returns them as the keys of <c>values</c> and <c>existing</c>.
/// </summary>
internal static class ImportTemplates
{
    public static readonly string[] Customers =
    [
        "customerCode", "erpNextId", "name", "nameAr", "taxId", "contactPerson",
        "contactPersonAr", "email", "phone", "city", "cityAr", "address",
        "addressAr", "defaultPaymentTerm", "defaultCurrency"
    ];

    public static readonly string[] Materials =
    [
        "itemCode", "erpNextItemCode", "name", "category", "referencePricePerKg",
        "uom", "isActive", "defaultWeightKg"
    ];

    public static readonly string[] Prices = ["itemCode", "referencePricePerKg"];

    public static string[] Columns(this ImportKind kind) => kind switch
    {
        ImportKind.Customers => Customers,
        ImportKind.Materials => Materials,
        _ => Prices
    };

    /// <summary>A header-only CSV; the UTF-8 BOM keeps Arabic values readable in Excel.</summary>
    public static byte[] CsvTemplate(this ImportKind kind)
    {
        var csv = string.Join(",", kind.Columns()) + "\r\n";
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        return [.. bom, .. System.Text.Encoding.UTF8.GetBytes(csv)];
    }
}
