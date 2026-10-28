using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sales_portal.core.DTOs;
using sales_portal.core.IService;

namespace sales_portal.api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<ActionResult<SettingsDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _settingsService.GetAsync(cancellationToken));
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SettingsDto>> Update(
        [FromBody] UpdateSettingsDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _settingsService.UpdateAsync(request, cancellationToken));
    }
}
