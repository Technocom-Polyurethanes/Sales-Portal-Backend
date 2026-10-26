using sales_portal.core.DTOs;
using sales_portal.core.Services;

namespace sales_portal.core.IService;

public interface IOfferCalculator
{
    OfferCalculateResponseDto Calculate(OfferDraftDto draft, OfferCatalog catalog);
}
