using System.Text.RegularExpressions;
using sales_portal.domain.Enums;

namespace sales_portal.core.Calculations;

/// <summary>
/// Format rules for offer references such as <c>Loc/42/YL/JD</c>.
/// The first segment is the offer type, the second the sequence, <c>YL</c> is the
/// static company identifier and the last segment is the creator's initials.
/// </summary>
public static class ReferenceRules
{
    public const string Pattern =
        @"^(?<type>Loc|Int)/(?<sequence>\d+)/YL/(?<initials>[A-Za-z0-9]+)$";

    public const string ExpectedFormat = "Loc/42/YL/JD";

    private static readonly Regex ReferenceRegex = new(
        Pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Builds a reference from its parts.</summary>
    public static string Build(OfferType type, int sequence, string initials)
        => $"{type}/{sequence}/YL/{initials.Trim().ToUpperInvariant()}";

    /// <summary>True when the reference follows the type/sequence/YL/initials format.</summary>
    public static bool IsValid(string? reference)
        => !string.IsNullOrWhiteSpace(reference) && ReferenceRegex.IsMatch(reference.Trim());

    /// <summary>
    /// Reads the sequence from a new-format reference. Legacy references such as
    /// <c>3089/OCT./YL/AA</c> fall back to their first numeric segment so the next
    /// sequence stays above already recorded offers.
    /// </summary>
    public static bool TryGetSequence(string? reference, out int sequence)
    {
        sequence = 0;

        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        var trimmed = reference.Trim();
        var match = ReferenceRegex.Match(trimmed);
        if (match.Success)
        {
            return int.TryParse(match.Groups["sequence"].Value, out sequence);
        }

        var firstSegment = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(firstSegment, out sequence);
    }

    /// <summary>
    /// Resolves the printed initials. The user's stored initials win; a user without
    /// them falls back to the first letters of their full name.
    /// </summary>
    public static string InitialsFrom(string? initials, string? fullName)
    {
        if (!string.IsNullOrWhiteSpace(initials))
        {
            // Keep the reference valid when stored initials contain dots or spaces.
            var cleaned = new string(initials.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (cleaned.Length > 0)
            {
                return cleaned;
            }
        }

        var words = (fullName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var derived = string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));

        return derived.Length > 0 ? derived : "NA";
    }

    /// <summary>
    /// Validates a reference against the format and the selected offer type.
    /// Returns <c>null</c> when the reference is empty or valid.
    /// </summary>
    public static string? Validate(string? reference, OfferType? offerType)
    {
        if (offerType == null)
        {
            return "Offer type is required.";
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var match = ReferenceRegex.Match(reference.Trim());
        if (!match.Success)
        {
            return $"Reference must follow the format {ExpectedFormat}.";
        }

        return string.Equals(
            match.Groups["type"].Value,
            offerType.Value.ToString(),
            StringComparison.OrdinalIgnoreCase)
            ? null
            : $"Reference must start with '{offerType.Value}' to match the offer type.";
    }
}
