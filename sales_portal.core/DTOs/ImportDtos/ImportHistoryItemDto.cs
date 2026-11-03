namespace sales_portal.core.DTOs;

public class ImportHistoryItemDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? CreatedByName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CommittedAt { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}
