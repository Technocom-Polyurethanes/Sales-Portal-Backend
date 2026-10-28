using sales_portal.core.DTOs;

namespace sales_portal.core.IService;

public interface IOfferService
{
    Task<OfferCalculateResponseDto> CalculateAsync(OfferDraftDto draft, Guid? currentUserId, CancellationToken cancellationToken);
    Task<PaginatedResultDto<OfferListItemDto>> GetListAsync(OfferQueryParamsDto query, CancellationToken cancellationToken);
    Task<OfferDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<OfferDocumentDto?> GetDocumentAsync(Guid id, CancellationToken cancellationToken);
    Task<List<OfferListItemDto>> GetVersionsAsync(Guid id, CancellationToken cancellationToken);
    Task<OfferNextSequenceDto> GetNextSequenceAsync(int year, int month, Guid? currentUserId, CancellationToken cancellationToken);
    Task<OfferDetailDto> CreateAsync(OfferDraftDto draft, Guid currentUserId, CancellationToken cancellationToken);
    Task<OfferDetailDto> UpdateAsync(Guid id, OfferDraftDto draft, Guid currentUserId, CancellationToken cancellationToken);
    Task<OfferDetailDto> ReviseAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken);
    Task<OfferDetailDto> UpdateStatusAsync(Guid id, UpdateOfferStatusDto dto, CancellationToken cancellationToken);
}
