namespace sales_portal.core.DTOs;

public class DocumentDeliveryMaterialCellDto
{
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<DocumentDeliveryPackagingLineDto> Packaging { get; set; } = new();
    public int TotalPackages { get; set; }
    public decimal TotalWeightKg { get; set; }
    public int OrderedPackages { get; set; }
    public decimal OrderedWeightKg { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public string DisplayTextAr { get; set; } = string.Empty;
}
