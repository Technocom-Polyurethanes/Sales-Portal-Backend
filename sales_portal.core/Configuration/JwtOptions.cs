namespace sales_portal.core.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "SalesPortalAPI";
    public string Audience { get; set; } = "SalesPortalClient";
    public int ExpiryHours { get; set; } = 12;
}
