using Microsoft.EntityFrameworkCore;
using sales_portal.core.Caching;
using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.IService;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;

namespace sales_portal.core.Services;

public class MaterialService : IMaterialService
{
    // The package type names are part of the cached rows, so settings changes drop them as well.
    private static readonly string[] MaterialRegions = [CacheRegions.Materials, CacheRegions.Catalog];

    private readonly IRepository<Material> _materials;
    private readonly IReferenceDataCache _cache;

    public MaterialService(IRepository<Material> materials, IReferenceDataCache? cache = null)
    {
        _materials = materials;
        _cache = cache ?? NoReferenceDataCache.Instance;
    }

    public async Task<PaginatedResultDto<MaterialLookupItemDto>> LookupAsync(
        LookupQueryDto query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var all = await LoadLookupListAsync();
        var matches = InMemoryQuery.Search(
            all,
            query.Search,
            m => m.ItemCode,
            m => m.Name,
            m => m.Category);

        return InMemoryQuery.Page(matches, page, pageSize);
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
        return (await FilterAsync(search, category, isActive)).ToList();
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

        var matches = await FilterAsync(search, category, isActive);
        return InMemoryQuery.Page(matches, page, pageSize);
    }

    private async Task<IReadOnlyList<MaterialDto>> FilterAsync(
        string? search,
        string? category,
        bool? isActive)
    {
        var all = await LoadListAsync();
        IReadOnlyList<MaterialDto> matches = InMemoryQuery.Search(
            all,
            search,
            m => m.ItemCode,
            m => m.Name,
            m => m.Category);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryTerm = category.Trim();
            matches = matches
                .Where(m => string.Equals(m.Category, categoryTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (isActive.HasValue)
        {
            matches = matches.Where(m => m.IsActive == isActive.Value).ToList();
        }

        return matches;
    }

    // Active materials for the picker, ordered by the item code that is shown (never by the GUID).
    private Task<IReadOnlyList<MaterialLookupItemDto>> LoadLookupListAsync()
    {
        return _cache.GetOrCreateAsync<IReadOnlyList<MaterialLookupItemDto>>(
            "materials:lookup",
            MaterialRegions,
            async () =>
            {
                var rows = await _materials.GetAll()
                    .AsNoTracking()
                    .Where(m => m.IsActive)
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
                    .ToListAsync(CancellationToken.None);

                rows.Sort((a, b) => NaturalStringComparer.Instance.Compare(a.ItemCode, b.ItemCode));
                return rows;
            });
    }

    // Every material (active or not), ordered by item code.
    private Task<IReadOnlyList<MaterialDto>> LoadListAsync()
    {
        return _cache.GetOrCreateAsync<IReadOnlyList<MaterialDto>>(
            "materials:all",
            MaterialRegions,
            async () =>
            {
                var rows = await BuildQuery(null, null, null).ToListAsync(CancellationToken.None);
                rows.Sort((a, b) => NaturalStringComparer.Instance.Compare(a.ItemCode, b.ItemCode));
                return rows;
            });
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
        _cache.Invalidate(CacheRegions.Materials);

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
        _cache.Invalidate(CacheRegions.Materials);

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
