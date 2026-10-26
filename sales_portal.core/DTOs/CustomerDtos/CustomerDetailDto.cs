namespace sales_portal.core.DTOs;

public class CustomerDetailDto : CustomerListItemDto
{
    public List<CustomerOfferHistoryDto> Offers { get; set; } = new();
}
