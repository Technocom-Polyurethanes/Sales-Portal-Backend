using sales_portal.core.DTOs;
using sales_portal.core.Services;

namespace sales_portal.core.IService;

public interface IOfferDocumentBuilder
{
    OfferDocumentDto Build(OfferDraftDto draft, OfferCatalog catalog, OfferCalculateResponseDto calculation);
}
