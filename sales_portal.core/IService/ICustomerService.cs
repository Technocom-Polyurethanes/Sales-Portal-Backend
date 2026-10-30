using sales_portal.core.DTOs;

namespace sales_portal.core.IService;

public interface ICustomerService
{
    Task<PaginatedResultDto<CustomerLookupItemDto>> LookupAsync(LookupQueryDto query, CancellationToken cancellationToken);
    Task<List<CustomerListItemDto>> GetAllAsync(string? search, CancellationToken cancellationToken);
    Task<PaginatedResultDto<CustomerListItemDto>> GetPagedAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<PaginatedResultDto<OfferListItemDto>> GetOffersAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken);
    Task<CustomerDetailDto?> GetByIdWithHistoryAsync(Guid id, CancellationToken cancellationToken);
    Task<CustomerDetailDto> CreateAsync(UpsertCustomerDto dto, CancellationToken cancellationToken);
    Task<CustomerDetailDto?> UpdateAsync(Guid id, UpsertCustomerDto dto, CancellationToken cancellationToken);
}
