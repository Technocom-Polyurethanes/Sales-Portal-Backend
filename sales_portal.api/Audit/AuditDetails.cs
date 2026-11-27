using System.Text.RegularExpressions;
using sales_portal.core.Audit;

namespace sales_portal.api.Audit;

/// <summary>Keeps details short and free of credentials before they leave the request.</summary>
public static class AuditDetails
{
    private const int MaxValueLength = 200;
    private static readonly string[] DeniedWords = ["password", "token", "secret", "hash"];
    private static readonly Regex UsernamePattern = new(
        @"^[A-Za-z0-9._-]{3,50}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static Dictionary<string, string> Sanitize(IReadOnlyDictionary<string, string>? details)
    {
        var clean = new Dictionary<string, string>(StringComparer.Ordinal);
        if (details is null) return clean;

        foreach (var (key, value) in details)
        {
            if (string.IsNullOrWhiteSpace(key) || DeniedWords.Any(word => key.Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            clean[Truncate(key, 60)] = Truncate(value ?? string.Empty, MaxValueLength);
        }

        return clean;
    }

    public static string SanitizeUsername(string? value)
    {
        var cleaned = Clean(value, 100);
        return UsernamePattern.IsMatch(cleaned) ? cleaned : string.Empty;
    }

    public static string SanitizeDescription(string? value) => Clean(value, 250);

    private static string Clean(string? value, int length) =>
        Truncate(new string((value ?? string.Empty)
            .Where(character => !char.IsControl(character))
            .ToArray()), length);

    public static string SanitizePath(string path)
    {
        var segments = path.Split('/').Select(segment =>
            Guid.TryParse(segment, out _) || int.TryParse(segment, out _) ? "{id}" : segment);
        return Truncate(string.Join('/', segments), 200);
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];
}
