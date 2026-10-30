using sales_portal.core.DTOs;

namespace sales_portal.core.IService;

public interface ISettingsService
{
    Task<SettingsDto> GetAsync(CancellationToken cancellationToken);
    Task<SettingsDto> UpdateAsync(UpdateSettingsDto dto, CancellationToken cancellationToken);
}
