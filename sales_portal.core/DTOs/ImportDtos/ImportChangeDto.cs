namespace sales_portal.core.DTOs;

public class ImportChangeDto
{
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
