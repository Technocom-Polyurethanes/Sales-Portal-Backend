namespace sales_portal.core.DTOs;

public class DocumentHeaderDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyNameAr { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string TaglineAr { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LogoUrl { get; set; }
    public decimal LogoWidthMm { get; set; }
    public decimal LogoHeightMm { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentTitleAr { get; set; } = string.Empty;
    public string FormCode { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public int Version { get; set; }
}
