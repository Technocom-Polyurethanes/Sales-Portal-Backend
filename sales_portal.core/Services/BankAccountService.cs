using Microsoft.EntityFrameworkCore;
using sales_portal.core.Caching;
using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.IService;
using sales_portal.domain.Interfaces;
using sales_portal.domain.Models;

namespace sales_portal.core.Services;

public class BankAccountService : IBankAccountService
{
    // Bank accounts belong to the shared catalog: settings and offer documents read them too.
    private static readonly string[] BankRegions = [CacheRegions.Catalog];

    private readonly IRepository<BankAccount> _bankAccounts;
    private readonly IReferenceDataCache _cache;

    public BankAccountService(IRepository<BankAccount> bankAccounts, IReferenceDataCache? cache = null)
    {
        _bankAccounts = bankAccounts;
        _cache = cache ?? NoReferenceDataCache.Instance;
    }

    public async Task<PaginatedResultDto<BankAccountDto>> GetPagedAsync(
        string? search,
        string? currency,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var all = await LoadListAsync();
        var matches = InMemoryQuery.Search(all, search, a => a.Bank, a => a.BankAr, a => a.Branch, a => a.AccountNumber);

        if (!string.IsNullOrWhiteSpace(currency))
        {
            var currencyTerm = currency.Trim();
            matches = matches
                .Where(a => string.Equals(a.Currency, currencyTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (isActive.HasValue)
        {
            matches = matches.Where(a => a.IsActive == isActive.Value).ToList();
        }

        return InMemoryQuery.Page(matches, page, pageSize);
    }

    public async Task<BankAccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _bankAccounts.GetAll()
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(MapProjection())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BankAccountDto> CreateAsync(UpsertBankAccountDto dto, CancellationToken cancellationToken)
    {
        Validate(dto);

        var account = new BankAccount();
        Apply(account, dto);
        if (account.SortOrder <= 0)
        {
            account.SortOrder = (await _bankAccounts.GetAll().MaxAsync(a => (int?)a.SortOrder, cancellationToken) ?? 0) + 1;
        }

        await _bankAccounts.AddAsync(account);
        await _bankAccounts.SaveChangesAsync(cancellationToken);
        _cache.Invalidate(CacheRegions.Catalog);

        return await GetByIdAsync(account.Id, cancellationToken)
            ?? throw new NotFoundException("Bank account was not found after creation.");
    }

    public async Task<BankAccountDto?> UpdateAsync(Guid id, UpsertBankAccountDto dto, CancellationToken cancellationToken)
    {
        Validate(dto);

        var account = await _bankAccounts.GetByIdAsync(id);
        if (account == null)
        {
            return null;
        }

        Apply(account, dto);
        if (account.SortOrder <= 0)
        {
            account.SortOrder = (await _bankAccounts.GetAll().MaxAsync(a => (int?)a.SortOrder, cancellationToken) ?? 0) + 1;
        }

        _bankAccounts.Update(account);
        await _bankAccounts.SaveChangesAsync(cancellationToken);
        _cache.Invalidate(CacheRegions.Catalog);

        return await GetByIdAsync(id, cancellationToken);
    }

    private Task<IReadOnlyList<BankAccountDto>> LoadListAsync()
    {
        return _cache.GetOrCreateAsync<IReadOnlyList<BankAccountDto>>(
            "banks:list",
            BankRegions,
            async () =>
            {
                var rows = await _bankAccounts.GetAll()
                    .AsNoTracking()
                    .OrderBy(a => a.SortOrder)
                    .ThenBy(a => a.Bank)
                    .Select(MapProjection())
                    .ToListAsync(CancellationToken.None);

                return rows;
            });
    }

    private static System.Linq.Expressions.Expression<Func<BankAccount, BankAccountDto>> MapProjection()
    {
        return a => new BankAccountDto
        {
            Id = a.Id,
            Bank = a.Bank,
            BankAr = a.BankAr,
            Branch = a.Branch,
            BranchAr = a.BranchAr,
            AccountHolderName = a.AccountHolderName,
            AccountHolderNameAr = a.AccountHolderNameAr,
            AccountNumber = a.AccountNumber,
            Swift = a.Swift,
            Iban = a.Iban,
            Currency = a.Currency,
            SortOrder = a.SortOrder,
            IsActive = a.IsActive
        };
    }

    private static void Apply(BankAccount target, UpsertBankAccountDto source)
    {
        target.Bank = source.Bank.Trim();
        target.BankAr = source.BankAr;
        target.Branch = source.Branch.Trim();
        target.BranchAr = source.BranchAr;
        target.AccountHolderName = source.AccountHolderName?.Trim() ?? string.Empty;
        target.AccountHolderNameAr = source.AccountHolderNameAr;
        target.AccountNumber = source.AccountNumber.Trim();
        target.Swift = source.Swift?.Trim() ?? string.Empty;
        target.Iban = source.Iban?.Trim() ?? string.Empty;
        target.Currency = string.IsNullOrWhiteSpace(source.Currency) ? "EGP" : source.Currency.Trim();
        target.SortOrder = source.SortOrder;
        target.IsActive = source.IsActive;
    }

    private static void Validate(UpsertBankAccountDto dto)
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

        if (string.IsNullOrWhiteSpace(dto.Bank))
        {
            AddError("bank", "Bank is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Branch))
        {
            AddError("branch", "Branch is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.AccountNumber))
        {
            AddError("accountNumber", "Account number is required.");
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("One or more validation errors occurred.", errors);
        }
    }
}
