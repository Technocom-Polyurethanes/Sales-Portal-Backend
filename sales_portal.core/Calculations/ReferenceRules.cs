using System.Text.RegularExpressions;

namespace sales_portal.core.Calculations;

/// <summary>
/// Format and month rules for offer references such as <c>3089/OCT./YL/AA</c>.
/// The month segment always follows the issue date. Matching is case-insensitive
/// so a manually typed <c>oct.</c> is accepted and compared by month.
/// </summary>
public static class ReferenceRules
{
    public static readonly IReadOnlyList<string> MonthAbbreviations =
    [
        "JAN.", "FEB.", "MAR.", "APR.", "MAY", "JUN.",
        "JUL.", "AUG.", "SEP.", "OCT.", "NOV.", "DEC."
    ];

    public const string Pattern =
        @"^\d+/(JAN\.|FEB\.|MAR\.|APR\.|MAY|JUN\.|JUL\.|AUG\.|SEP\.|OCT\.|NOV\.|DEC\.)/[A-Za-z0-9_-]+/[A-Za-z0-9_-]+$";

    public const string ExpectedFormat = "100/OCT./YL/AA";

    private static readonly Regex ReferenceRegex = new(
        Pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MonthRegex = new(
        @"^\d+/(?<month>JAN\.|FEB\.|MAR\.|APR\.|MAY|JUN\.|JUL\.|AUG\.|SEP\.|OCT\.|NOV\.|DEC\.)/[A-Za-z0-9_-]+/[A-Za-z0-9_-]+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Returns the month abbreviation for a 1-based month number.</summary>
    public static string MonthAbbreviation(int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be between 1 and 12.");
        }

        return MonthAbbreviations[month - 1];
    }

    /// <summary>Builds a reference from its parts using the configured prefix and user initials.</summary>
    public static string Build(int sequence, DateOnly issueDate, string prefix, string initials)
        => $"{sequence}/{MonthAbbreviation(issueDate.Month)}/{prefix}/{initials}";

    /// <summary>True when the reference follows the sequence/month/prefix/initials format.</summary>
    public static bool IsValid(string? reference)
        => !string.IsNullOrWhiteSpace(reference) && ReferenceRegex.IsMatch(reference.Trim());

    /// <summary>
    /// Returns the normalized (uppercase) month segment when the reference matches the format.
    /// </summary>
    public static bool TryGetMonth(string? reference, out string month)
    {
        month = string.Empty;

        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var match = MonthRegex.Match(reference.Trim());
        if (!match.Success)
        {
            return false;
        }

        month = match.Groups["month"].Value.ToUpperInvariant();
        return true;
    }

    /// <summary>
    /// Validates an optional reference against the format and the issue date month.
    /// Returns <c>null</c> when the reference is empty or valid.
    /// </summary>
    public static string? Validate(string? reference, DateOnly issueDate)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        if (!TryGetMonth(reference, out var month))
        {
            return $"Reference must follow the format {ExpectedFormat}.";
        }

        var expectedMonth = MonthAbbreviation(issueDate.Month);
        return month == expectedMonth
            ? null
            : $"Reference month ({month}) does not match the issue date month ({expectedMonth}).";
    }
}
