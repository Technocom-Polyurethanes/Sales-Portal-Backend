using sales_portal.core.DTOs;

namespace sales_portal.core.IService;

public interface IBankAccountService
{
    Task<PaginatedResultDto<BankAccountDto>> GetPagedAsync(
        string? search,
        string? currency,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<BankAccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<BankAccountDto> CreateAsync(UpsertBankAccountDto dto, CancellationToken cancellationToken);
    Task<BankAccountDto?> UpdateAsync(Guid id, UpsertBankAccountDto dto, CancellationToken cancellationToken);
}
