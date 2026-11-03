namespace sales_portal.core.DTOs;

public class ImportRowDto
{
    public int RowNumber { get; set; }

    /// <summary>new | update | unchanged | error</summary>
    public string Status { get; set; } = "error";
    public List<string> Errors { get; set; } = new();

    /// <summary>Parsed row keyed by the template column name.</summary>
    public Dictionary<string, string?> Values { get; set; } = new();

    /// <summary>The stored entity values when the row updates an existing record.</summary>
    public Dictionary<string, string?>? Existing { get; set; }
    public List<ImportChangeDto> Changes { get; set; } = new();
}
