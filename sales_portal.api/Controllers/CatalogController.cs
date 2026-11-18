using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sales_portal.api.Authorization;
using sales_portal.core.DTOs;
using sales_portal.core.IService;

namespace sales_portal.api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CatalogController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet("bootstrap")]
    public async Task<ActionResult<CatalogBootstrapDto>> Bootstrap(CancellationToken cancellationToken)
    {
        return Ok(await _catalogService.GetBootstrapAsync(cancellationToken));
    }

    [HttpPost("payment-terms")]
    [Authorize(Policy = Policies.CatalogCreate)]
    public async Task<ActionResult<PaymentTermDto>> CreatePaymentTerm(
        [FromBody] UpsertPaymentTermDto request,
        CancellationToken cancellationToken)
    {
        var term = await _catalogService.CreatePaymentTermAsync(request, cancellationToken);
        return Created($"api/catalog/payment-terms/{term.Id}", term);
    }

    [HttpPut("payment-terms/{id:guid}")]
    [Authorize(Policy = Policies.CatalogEdit)]
    public async Task<ActionResult<PaymentTermDto>> UpdatePaymentTerm(
        Guid id,
        [FromBody] UpsertPaymentTermDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _catalogService.UpdatePaymentTermAsync(id, request, cancellationToken));
    }
}
