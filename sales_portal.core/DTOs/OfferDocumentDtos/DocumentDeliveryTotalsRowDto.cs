namespace sales_portal.core.DTOs;

public class DocumentDeliveryTotalsRowDto
{
    public int TotalPackages { get; set; }
    public decimal TotalWeightKg { get; set; }
    public List<DocumentDeliveryPackagingSummaryDto> PackagingSummary { get; set; } = new();
    public IReadOnlyList<DocumentDeliveryMaterialCellDto> Materials { get; set; } = [];
}
