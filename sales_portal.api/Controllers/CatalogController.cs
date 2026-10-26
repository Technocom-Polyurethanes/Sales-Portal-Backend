using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
