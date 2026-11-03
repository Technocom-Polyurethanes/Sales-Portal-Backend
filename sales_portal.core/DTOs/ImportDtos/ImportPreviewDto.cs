namespace sales_portal.core.DTOs;

public class ImportPreviewDto
{
    public Guid ImportId { get; set; }
    public ImportSummaryDto Summary { get; set; } = new();
    public List<ImportRowDto> Rows { get; set; } = new();
}
