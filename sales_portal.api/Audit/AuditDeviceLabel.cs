namespace sales_portal.api.Audit;

/// <summary>Tiny User-Agent reader for the audit UI; the raw header stays in the row.</summary>
public static class AuditDeviceLabel
{
    public static string For(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown device";

        var browser = new[]
        {
            ("Edg/", "Edge"), ("OPR/", "Opera"), ("Chrome/", "Chrome"), ("CriOS/", "Chrome"),
            ("Firefox/", "Firefox"), ("FxiOS/", "Firefox"), ("Safari/", "Safari")
        }
        .Select(marker => (Name: marker.Item2, Version: VersionAfter(userAgent, marker.Item1)))
        .FirstOrDefault(candidate => candidate.Version is not null);

        var operatingSystem = Contains(userAgent, "Windows") ? "Windows"
            : Contains(userAgent, "Android") ? "Android"
            : Contains(userAgent, "iPhone") || Contains(userAgent, "iPad") ? "iOS"
            : Contains(userAgent, "Mac OS X") ? "macOS"
            : Contains(userAgent, "Linux") ? "Linux"
            : null;

        if (browser.Name is null || operatingSystem is null) return "Unknown device";
        return $"{browser.Name} {browser.Version} on {operatingSystem}";
    }

    private static string? VersionAfter(string userAgent, string marker)
    {
        var start = userAgent.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;

        var position = start + marker.Length;
        var end = position;
        while (end < userAgent.Length && (char.IsDigit(userAgent[end]) || userAgent[end] == '.')) end++;

        if (end == position) return null;
        var version = userAgent[position..end];
        var majorEnd = version.IndexOf('.');
        return majorEnd < 0 ? version : version[..majorEnd];
    }

    private static bool Contains(string value, string marker) =>
        value.Contains(marker, StringComparison.OrdinalIgnoreCase);
}
