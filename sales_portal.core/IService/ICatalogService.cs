using sales_portal.core.DTOs;
using sales_portal.core.Services;

namespace sales_portal.core.IService;

public interface ICatalogService
{
    Task<CatalogBootstrapDto> GetBootstrapAsync(CancellationToken cancellationToken);
    Task<OfferCatalog> GetOfferCatalogAsync(Guid? customerId, Guid? createdByUserId, CancellationToken cancellationToken);
}
