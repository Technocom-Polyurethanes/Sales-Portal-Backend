using sales_portal.core.DTOs;

namespace sales_portal.core.IService;

public interface IImportService
{
    Task<ImportTemplateDto> GetTemplateAsync(string type, CancellationToken cancellationToken);

    Task<ImportPreviewDto> PreviewAsync(
        string type,
        Stream content,
        string fileName,
        long length,
        Guid? userId,
        CancellationToken cancellationToken);

    Task<ImportCommitResultDto> CommitAsync(Guid importId, CancellationToken cancellationToken);

    Task<PaginatedResultDto<ImportHistoryItemDto>> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
