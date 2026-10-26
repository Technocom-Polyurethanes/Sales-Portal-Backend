using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sales_portal.core.DTOs;
using sales_portal.core.IService;

namespace sales_portal.api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetCustomers(
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page.HasValue || pageSize.HasValue)
        {
            return Ok(await _customerService.GetPagedAsync(
                search,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken));
        }

        return Ok(await _customerService.GetAllAsync(search, cancellationToken));
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<PaginatedResultDto<CustomerLookupItemDto>>> Lookup(
        [FromQuery] LookupQueryDto query,
        CancellationToken cancellationToken)
    {
        return Ok(await _customerService.LookupAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDetailDto>> GetCustomer(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customer = await _customerService.GetByIdWithHistoryAsync(id, cancellationToken);
        return customer == null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDetailDto>> CreateCustomer(
        [FromBody] UpsertCustomerDto request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDetailDto>> UpdateCustomer(
        Guid id,
        [FromBody] UpsertCustomerDto request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerService.UpdateAsync(id, request, cancellationToken);
        return customer == null ? NotFound() : Ok(customer);
    }
}
