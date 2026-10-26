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
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetCustomers(
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.CustomerCode.ToLower().Contains(term) ||
                c.Name.ToLower().Contains(term) ||
                (c.NameAr != null && c.NameAr.ToLower().Contains(term)));
        }

        var projected = query
            .OrderBy(c => c.Name)
            .Select(c => new CustomerDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                ErpNextId = c.ErpNextId,
                Name = c.Name,
                NameAr = c.NameAr,
                TaxId = c.TaxId,
                ContactPerson = c.ContactPerson,
                Email = c.Email,
                Phone = c.Phone,
                City = c.City,
                Address = c.Address,
                DefaultPaymentTermId = c.DefaultPaymentTermId,
                DefaultPaymentTermLabel = c.DefaultPaymentTerm != null ? c.DefaultPaymentTerm.Label : null,
                DefaultCurrency = c.DefaultCurrency,
                OffersCount = c.Offers.Count,
                TotalValue = c.Offers.Sum(o => (decimal?)o.Total) ?? 0m,
                LastOfferDate = c.Offers
                    .OrderByDescending(o => o.IssueDate)
                    .Select(o => (DateOnly?)o.IssueDate)
                    .FirstOrDefault()
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

            return Ok(new PagedResult<CustomerDto>
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
    public async Task<ActionResult<PagedResult<CustomerLookupDto>>> Lookup(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.CustomerCode.ToLower().Contains(term) ||
                c.Name.ToLower().Contains(term) ||
                (c.NameAr != null && c.NameAr.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerLookupDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                Name = c.Name,
                NameAr = c.NameAr,
                ContactPerson = c.ContactPerson,
                City = c.City,
                TaxId = c.TaxId,
                DefaultCurrency = c.DefaultCurrency,
                Email = c.Email,
                Phone = c.Phone
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<CustomerLookupDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDetailsDto>> GetCustomer(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .Include(c => c.DefaultPaymentTerm)
            .Include(c => c.Offers)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (customer == null)
        {
            return NotFound();
        }

        return Ok(MapCustomerDetails(customer));
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDetailsDto>> CreateCustomer(
        [FromBody] CustomerUpsertDto request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateCustomer(request);
        if (validation != null)
        {
            return BadRequest(new { message = validation });
        }

        var customerCode = request.CustomerCode.Trim();
        if (await _db.Customers.AnyAsync(c => c.CustomerCode == customerCode, cancellationToken))
        {
            return Conflict(new { message = "A customer with this customer code already exists." });
        }

        var customer = new Customer
        {
            CustomerCode = customerCode,
            ErpNextId = request.ErpNextId,
            Name = request.Name.Trim(),
            NameAr = request.NameAr,
            TaxId = request.TaxId,
            ContactPerson = request.ContactPerson,
            Email = request.Email,
            Phone = request.Phone,
            City = request.City,
            Address = request.Address.Trim(),
            DefaultPaymentTermId = request.DefaultPaymentTermId,
            DefaultCurrency = string.IsNullOrWhiteSpace(request.DefaultCurrency) ? "EGP" : request.DefaultCurrency,
            Offers = new List<Offer>()
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, MapCustomerDetails(customer));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDetailsDto>> UpdateCustomer(
        Guid id,
        [FromBody] CustomerUpsertDto request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateCustomer(request);
        if (validation != null)
        {
            return BadRequest(new { message = validation });
        }

        var customer = await _db.Customers
            .Include(c => c.DefaultPaymentTerm)
            .Include(c => c.Offers)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (customer == null)
        {
            return NotFound();
        }

        var customerCode = request.CustomerCode.Trim();
        if (await _db.Customers.AnyAsync(
                c => c.Id != id && c.CustomerCode == customerCode,
                cancellationToken))
        {
            return Conflict(new { message = "A customer with this customer code already exists." });
        }

        customer.CustomerCode = customerCode;
        customer.ErpNextId = request.ErpNextId;
        customer.Name = request.Name.Trim();
        customer.NameAr = request.NameAr;
        customer.TaxId = request.TaxId;
        customer.ContactPerson = request.ContactPerson;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.City = request.City;
        customer.Address = request.Address.Trim();
        customer.DefaultPaymentTermId = request.DefaultPaymentTermId;
        customer.DefaultCurrency = string.IsNullOrWhiteSpace(request.DefaultCurrency) ? "EGP" : request.DefaultCurrency;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapCustomerDetails(customer));
    }

    private static string? ValidateCustomer(CustomerUpsertDto request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerCode))
        {
            return "Customer code is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Customer name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return "Customer address is required.";
        }

        return null;
    }

    private static CustomerDetailsDto MapCustomerDetails(Customer customer)
    {
        var offers = customer.Offers
            .OrderByDescending(o => o.IssueDate)
            .ThenByDescending(o => o.Reference)
            .ToList();

        return new CustomerDetailsDto
        {
            Id = customer.Id,
            CustomerCode = customer.CustomerCode,
            ErpNextId = customer.ErpNextId,
            Name = customer.Name,
            NameAr = customer.NameAr,
            TaxId = customer.TaxId,
            ContactPerson = customer.ContactPerson,
            Email = customer.Email,
            Phone = customer.Phone,
            City = customer.City,
            Address = customer.Address,
            DefaultPaymentTermId = customer.DefaultPaymentTermId,
            DefaultPaymentTermLabel = customer.DefaultPaymentTerm?.Label,
            DefaultCurrency = customer.DefaultCurrency,
            OffersCount = offers.Count,
            TotalValue = offers.Sum(o => o.Total),
            LastOfferDate = offers.Count == 0 ? null : offers.Max(o => o.IssueDate),
            Offers = offers.Select(o => new CustomerOfferHistoryDto
            {
                Id = o.Id,
                Reference = o.Reference,
                Version = o.Version,
                IssueDate = o.IssueDate,
                Status = o.Status,
                Total = o.Total,
                Currency = o.Currency
            }).ToList()
        };
    }
}
