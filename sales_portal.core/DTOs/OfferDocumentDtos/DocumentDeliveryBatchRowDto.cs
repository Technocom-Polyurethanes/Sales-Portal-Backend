namespace sales_portal.core.DTOs;

public class DocumentDeliveryBatchRowDto
{
    public int BatchNumber { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public string DayOfWeekAr { get; set; } = string.Empty;
    public List<DocumentDeliveryMaterialCellDto> Materials { get; set; } = new();
    public List<DocumentDeliveryPackagingSummaryDto> PackagingSummary { get; set; } = new();
    public int TotalPackages { get; set; }
    public decimal TotalWeightKg { get; set; }
}
