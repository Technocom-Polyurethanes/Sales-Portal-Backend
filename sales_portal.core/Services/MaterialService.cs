using Microsoft.EntityFrameworkCore;
using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.IService;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;

namespace sales_portal.core.Services;

public class MaterialService : IMaterialService
{
    private readonly IRepository<Material> _materials;

    public MaterialService(IRepository<Material> materials)
    {
        _materials = materials;
    }

    public async Task<PaginatedResultDto<MaterialLookupItemDto>> LookupAsync(
        LookupQueryDto query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var materials = BuildSearchQuery(query.Search)
            .Where(m => m.IsActive);

        var totalCount = await materials.CountAsync(cancellationToken);
        var items = await materials
            .OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MaterialLookupItemDto
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

        return new PaginatedResultDto<MaterialLookupItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public Task<List<MaterialDto>> GetAllAsync(
        string? search,
        string? category,
        CancellationToken cancellationToken)
    {
        return GetAllAsync(search, category, isActive: null, cancellationToken);
    }

    public async Task<List<MaterialDto>> GetAllAsync(
        string? search,
        string? category,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        return await BuildQuery(search, category, isActive)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaginatedResultDto<MaterialDto>> GetPagedAsync(
        string? search,
        string? category,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = BuildSearchQuery(search);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryTerm = category.Trim().ToLowerInvariant();
            query = query.Where(m => m.Category.ToLower() == categoryTerm);
        }

        if (isActive.HasValue)
        {
            query = query.Where(m => m.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await BuildQuery(search, category, isActive)
            .OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResultDto<MaterialDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<MaterialDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _materials.GetAll()
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(MapProjection())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MaterialDto> CreateAsync(
        UpsertMaterialDto dto,
        CancellationToken cancellationToken)
    {
        Validate(dto);

        var itemCode = dto.ItemCode.Trim();
        if (await _materials.GetAll().AnyAsync(m => m.ItemCode == itemCode, cancellationToken))
        {
            throw new ConflictException("A material with this item code already exists.");
        }

        var material = new Material
        {
            ItemCode = itemCode,
            ErpNextItemCode = dto.ErpNextItemCode,
            Name = dto.Name.Trim(),
            Category = dto.Category.Trim(),
            ReferencePricePerKg = dto.ReferencePricePerKg,
            Uom = string.IsNullOrWhiteSpace(dto.Uom) ? "kg" : dto.Uom,
            DefaultPackageTypeId = dto.DefaultPackageTypeId,
            DefaultWeightKg = dto.DefaultWeightKg,
            IsActive = dto.IsActive
        };

        await _materials.AddAsync(material);
        await _materials.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(material.Id, cancellationToken)
            ?? throw new NotFoundException("Material was not found after creation.");
    }

    public async Task<MaterialDto?> UpdateAsync(
        Guid id,
        UpsertMaterialDto dto,
        CancellationToken cancellationToken)
    {
        Validate(dto);

        var material = await _materials.GetByIdAsync(id);
        if (material == null)
        {
            return null;
        }

        var itemCode = dto.ItemCode.Trim();
        if (await _materials.GetAll().AnyAsync(
                m => m.Id != id && m.ItemCode == itemCode,
                cancellationToken))
        {
            throw new ConflictException("A material with this item code already exists.");
        }

        material.ItemCode = itemCode;
        material.ErpNextItemCode = dto.ErpNextItemCode;
        material.Name = dto.Name.Trim();
        material.Category = dto.Category.Trim();
        material.ReferencePricePerKg = dto.ReferencePricePerKg;
        material.Uom = string.IsNullOrWhiteSpace(dto.Uom) ? "kg" : dto.Uom;
        material.DefaultPackageTypeId = dto.DefaultPackageTypeId;
        material.DefaultWeightKg = dto.DefaultWeightKg;
        material.IsActive = dto.IsActive;

        _materials.Update(material);
        await _materials.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(material.Id, cancellationToken);
    }

    private IQueryable<Material> BuildSearchQuery(string? search)
    {
        var query = _materials.GetAll().AsNoTracking().AsQueryable();
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var term = search.Trim().ToLowerInvariant();
        return query.Where(m =>
            m.ItemCode.ToLower().Contains(term) ||
            m.Name.ToLower().Contains(term) ||
            m.Category.ToLower().Contains(term));
    }

    private IQueryable<MaterialDto> BuildQuery(string? search, string? category, bool? isActive)
    {
        var query = BuildSearchQuery(search);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryTerm = category.Trim().ToLowerInvariant();
            query = query.Where(m => m.Category.ToLower() == categoryTerm);
        }

        if (isActive.HasValue)
        {
            query = query.Where(m => m.IsActive == isActive.Value);
        }

        return query.Select(MapProjection());
    }

    private static System.Linq.Expressions.Expression<Func<Material, MaterialDto>> MapProjection()
    {
        return m => new MaterialDto
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
        };
    }

    private static void Validate(UpsertMaterialDto dto)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        void AddError(string field, string message)
        {
            if (errors.TryGetValue(field, out var existing))
            {
                errors[field] = existing.Append(message).ToArray();
            }
            else
            {
                errors[field] = [message];
            }
        }

        if (string.IsNullOrWhiteSpace(dto.ItemCode))
        {
            AddError("itemCode", "Item code is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            AddError("name", "Material name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Category))
        {
            AddError("category", "Material category is required.");
        }

        if (dto.DefaultWeightKg < 0m)
        {
            AddError("defaultWeightKg", "Default weight cannot be negative.");
        }

        if (dto.ReferencePricePerKg.HasValue && dto.ReferencePricePerKg.Value < 0m)
        {
            AddError("referencePricePerKg", "Reference price per kg cannot be negative.");
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("One or more validation errors occurred.", errors);
        }
    }
}
