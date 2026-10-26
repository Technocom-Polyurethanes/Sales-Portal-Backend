namespace sales_portal.core.DTOs;

public class CompanySettingsDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string TaglineAr { get; set; } = string.Empty;
    public string FormCode { get; set; } = string.Empty;
    public string ReferencePrefix { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LogoUrl { get; set; }
    public decimal LogoWidthMm { get; set; }
    public decimal LogoHeightMm { get; set; }
}
