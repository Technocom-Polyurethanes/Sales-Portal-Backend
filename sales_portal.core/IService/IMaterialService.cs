using sales_portal.core.DTOs;

namespace sales_portal.core.IService;

public interface IMaterialService
{
    Task<PaginatedResultDto<MaterialLookupItemDto>> LookupAsync(LookupQueryDto query, CancellationToken cancellationToken);
    Task<List<MaterialDto>> GetAllAsync(string? search, string? category, CancellationToken cancellationToken);
    Task<List<MaterialDto>> GetAllAsync(string? search, string? category, bool? isActive, CancellationToken cancellationToken);
    Task<PaginatedResultDto<MaterialDto>> GetPagedAsync(string? search, string? category, bool? isActive, int page, int pageSize, CancellationToken cancellationToken);
    Task<MaterialDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<MaterialDto> CreateAsync(UpsertMaterialDto dto, CancellationToken cancellationToken);
    Task<MaterialDto?> UpdateAsync(Guid id, UpsertMaterialDto dto, CancellationToken cancellationToken);
}
