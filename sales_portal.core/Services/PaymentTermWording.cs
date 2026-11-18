namespace sales_portal.core.Services;

public static class PaymentTermWording
{
    public const string DaysToken = "{days}";

    /// <summary>The conditions text with the number of days filled in; blank text stays null.</summary>
    public static string? Apply(string? conditions, int? days)
    {
        if (string.IsNullOrWhiteSpace(conditions))
        {
            return null;
        }

        return conditions.Trim().Replace(DaysToken, (days ?? 0).ToString(), StringComparison.Ordinal);
    }
}
