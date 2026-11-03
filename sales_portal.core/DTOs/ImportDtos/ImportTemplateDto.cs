namespace sales_portal.core.DTOs;

public class ImportTemplateDto
{
    public byte[] Content { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/csv";
}
