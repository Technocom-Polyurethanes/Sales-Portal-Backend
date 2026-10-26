namespace sales_portal.core.DTOs;

public class CalculatedDeliveryBatchDto
{
    public Guid Id { get; set; }
    public int BatchNumber { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public List<CalculatedDeliveryAllocationDto> Allocations { get; set; } = new();
    public List<DeliveryPackagingSummaryDto> PackagingSummary { get; set; } = new();
    public int TotalPackages { get; set; }
    public decimal TotalWeightKg { get; set; }
}
