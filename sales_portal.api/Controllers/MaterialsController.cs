using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sales_portal.core.DTOs;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;

namespace sales_portal.api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class MaterialsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MaterialsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetMaterials(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Materials.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(m =>
                m.ItemCode.ToLower().Contains(term) ||
                m.Name.ToLower().Contains(term) ||
                m.Category.ToLower().Contains(term));
        }

        if (isActive.HasValue)
        {
            query = query.Where(m => m.IsActive == isActive.Value);
        }

        var projected = query
            .OrderBy(m => m.Name)
            .Select(m => new MaterialDto
            {
                Id = m.Id,
                ItemCode = m.ItemCode,
                ErpNextItemCode = m.ErpNextItemCode,
                Name = m.Name,
                Category = m.Category,
                ReferencePricePerKg = m.ReferencePricePerKg,
                Uom = m.Uom,
                DefaultPackageTypeId = m.DefaultPackageTypeId,
                DefaultPackageTypeName = m.DefaultPackageType != null ? m.DefaultPackageType.Name : null,
                DefaultPackageTypeNameAr = m.DefaultPackageType != null ? m.DefaultPackageType.NameAr : null,
                DefaultWeightKg = m.DefaultWeightKg,
                IsActive = m.IsActive
            });

        if (page.HasValue || pageSize.HasValue)
        {
            var currentPage = Math.Max(1, page ?? 1);
            var currentPageSize = Math.Clamp(pageSize ?? 20, 1, 200);
            var totalCount = await query.CountAsync(cancellationToken);
            var items = await projected
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .ToListAsync(cancellationToken);

            return Ok(new PagedResult<MaterialDto>
            {
                Items = items,
                Page = currentPage,
                PageSize = currentPageSize,
                TotalCount = totalCount
            });
        }

        return Ok(await projected.ToListAsync(cancellationToken));
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<PagedResult<MaterialLookupDto>>> Lookup(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Materials.AsNoTracking().Where(m => m.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(m =>
                m.ItemCode.ToLower().Contains(term) ||
                m.Name.ToLower().Contains(term) ||
                m.Category.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MaterialLookupDto
            {
                Id = m.Id,
                ItemCode = m.ItemCode,
                Name = m.Name,
                Category = m.Category,
                ReferencePricePerKg = m.ReferencePricePerKg,
                Uom = m.Uom,
                DefaultPackageTypeId = m.DefaultPackageTypeId,
                DefaultPackageTypeName = m.DefaultPackageType != null ? m.DefaultPackageType.Name : null,
                DefaultPackageTypeNameAr = m.DefaultPackageType != null ? m.DefaultPackageType.NameAr : null,
                DefaultWeightKg = m.DefaultWeightKg,
                IsActive = m.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<MaterialLookupDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaterialDto>> GetMaterial(Guid id, CancellationToken cancellationToken)
    {
        var material = await LoadMaterialDto(id, cancellationToken);
        return material == null ? NotFound() : Ok(material);
    }

    [HttpPost]
    public async Task<ActionResult<MaterialDto>> CreateMaterial(
        [FromBody] MaterialUpsertDto request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateMaterial(request);
        if (validation != null)
        {
            return BadRequest(new { message = validation });
        }

        var itemCode = request.ItemCode.Trim();
        if (await _db.Materials.AnyAsync(m => m.ItemCode == itemCode, cancellationToken))
        {
            return Conflict(new { message = "A material with this item code already exists." });
        }

        var material = new Material
        {
            ItemCode = itemCode,
            ErpNextItemCode = request.ErpNextItemCode,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            ReferencePricePerKg = request.ReferencePricePerKg,
            Uom = string.IsNullOrWhiteSpace(request.Uom) ? "kg" : request.Uom,
            DefaultPackageTypeId = request.DefaultPackageTypeId,
            DefaultWeightKg = request.DefaultWeightKg,
            IsActive = request.IsActive
        };

        _db.Materials.Add(material);
        await _db.SaveChangesAsync(cancellationToken);

        var created = await LoadMaterialDto(material.Id, cancellationToken);
        return CreatedAtAction(nameof(GetMaterial), new { id = material.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaterialDto>> UpdateMaterial(
        Guid id,
        [FromBody] MaterialUpsertDto request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateMaterial(request);
        if (validation != null)
        {
            return BadRequest(new { message = validation });
        }

        var material = await _db.Materials.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (material == null)
        {
            return NotFound();
        }

        var itemCode = request.ItemCode.Trim();
        if (await _db.Materials.AnyAsync(
                m => m.Id != id && m.ItemCode == itemCode,
                cancellationToken))
        {
            return Conflict(new { message = "A material with this item code already exists." });
        }

        material.ItemCode = itemCode;
        material.ErpNextItemCode = request.ErpNextItemCode;
        material.Name = request.Name.Trim();
        material.Category = request.Category.Trim();
        material.ReferencePricePerKg = request.ReferencePricePerKg;
        material.Uom = string.IsNullOrWhiteSpace(request.Uom) ? "kg" : request.Uom;
        material.DefaultPackageTypeId = request.DefaultPackageTypeId;
        material.DefaultWeightKg = request.DefaultWeightKg;
        material.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await LoadMaterialDto(material.Id, cancellationToken));
    }

    private async Task<MaterialDto?> LoadMaterialDto(Guid id, CancellationToken cancellationToken)
    {
        return await _db.Materials
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new MaterialDto
            {
                Id = m.Id,
                ItemCode = m.ItemCode,
                ErpNextItemCode = m.ErpNextItemCode,
                Name = m.Name,
                Category = m.Category,
                ReferencePricePerKg = m.ReferencePricePerKg,
                Uom = m.Uom,
                DefaultPackageTypeId = m.DefaultPackageTypeId,
                DefaultPackageTypeName = m.DefaultPackageType != null ? m.DefaultPackageType.Name : null,
                DefaultPackageTypeNameAr = m.DefaultPackageType != null ? m.DefaultPackageType.NameAr : null,
                DefaultWeightKg = m.DefaultWeightKg,
                IsActive = m.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? ValidateMaterial(MaterialUpsertDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ItemCode))
        {
            return "Item code is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Material name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return "Material category is required.";
        }

        if (request.DefaultWeightKg < 0m)
        {
            return "Default weight cannot be negative.";
        }

        return null;
    }
}
