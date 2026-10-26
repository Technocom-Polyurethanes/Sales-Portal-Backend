using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sales_portal.core.DTOs;
using sales_portal.core.IService;

namespace sales_portal.api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class MaterialsController : ControllerBase
{
    private readonly IMaterialService _materialService;

    public MaterialsController(IMaterialService materialService)
    {
        _materialService = materialService;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetMaterials(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] bool? isActive,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page.HasValue || pageSize.HasValue)
        {
            return Ok(await _materialService.GetPagedAsync(
                search,
                category,
                isActive,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken));
        }

        return Ok(await _materialService.GetAllAsync(
            search,
            category,
            isActive,
            cancellationToken));
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<PaginatedResultDto<MaterialLookupItemDto>>> Lookup(
        [FromQuery] LookupQueryDto query,
        CancellationToken cancellationToken)
    {
        return Ok(await _materialService.LookupAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaterialDto>> GetMaterial(
        Guid id,
        CancellationToken cancellationToken)
    {
        var material = await _materialService.GetByIdAsync(id, cancellationToken);
        return material == null ? NotFound() : Ok(material);
    }

    [HttpPost]
    public async Task<ActionResult<MaterialDto>> CreateMaterial(
        [FromBody] UpsertMaterialDto request,
        CancellationToken cancellationToken)
    {
        var material = await _materialService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetMaterial), new { id = material.Id }, material);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaterialDto>> UpdateMaterial(
        Guid id,
        [FromBody] UpsertMaterialDto request,
        CancellationToken cancellationToken)
    {
        var material = await _materialService.UpdateAsync(id, request, cancellationToken);
        return material == null ? NotFound() : Ok(material);
    }
}
