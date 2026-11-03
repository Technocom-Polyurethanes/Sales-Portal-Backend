namespace sales_portal.core.DTOs;

public class ImportSummaryDto
{
    public int Total { get; set; }
    public int New { get; set; }
    public int Update { get; set; }
    public int Unchanged { get; set; }
    public int Error { get; set; }
}
