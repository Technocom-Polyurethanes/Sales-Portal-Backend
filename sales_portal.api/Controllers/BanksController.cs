using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sales_portal.api.Authorization;
using sales_portal.core.DTOs;
using sales_portal.core.IService;

namespace sales_portal.api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class BanksController : ControllerBase
{
    private readonly IBankAccountService _bankAccountService;

    public BanksController(IBankAccountService bankAccountService)
    {
        _bankAccountService = bankAccountService;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResultDto<BankAccountDto>>> GetBanks(
        [FromQuery] string? search,
        [FromQuery] string? currency,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _bankAccountService.GetPagedAsync(
            search,
            currency,
            isActive,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BankAccountDto>> GetBank(
        Guid id,
        CancellationToken cancellationToken)
    {
        var account = await _bankAccountService.GetByIdAsync(id, cancellationToken);
        return account == null ? NotFound() : Ok(account);
    }

    [HttpPost]
    [Authorize(Policy = Policies.CatalogCreate)]
    public async Task<ActionResult<BankAccountDto>> CreateBank(
        [FromBody] UpsertBankAccountDto request,
        CancellationToken cancellationToken)
    {
        var account = await _bankAccountService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetBank), new { id = account.Id }, account);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CatalogEdit)]
    public async Task<ActionResult<BankAccountDto>> UpdateBank(
        Guid id,
        [FromBody] UpsertBankAccountDto request,
        CancellationToken cancellationToken)
    {
        var account = await _bankAccountService.UpdateAsync(id, request, cancellationToken);
        return account == null ? NotFound() : Ok(account);
    }
}
