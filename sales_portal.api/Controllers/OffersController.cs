using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sales_portal.core.DTOs;
using sales_portal.core.IService;

namespace sales_portal.api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OffersController : ControllerBase
{
    private readonly IOfferService _offerService;

    public OffersController(IOfferService offerService)
    {
        _offerService = offerService;
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<OfferCalculateResponseDto>> Calculate(
        [FromBody] OfferDraftDto request,
        CancellationToken cancellationToken)
    {
        request.CreatedByUserId ??= CurrentUserId();
        return Ok(await _offerService.CalculateAsync(request, CurrentUserId(), cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResultDto<OfferListItemDto>>> GetOffers(
        [FromQuery] OfferQueryParamsDto query,
        CancellationToken cancellationToken)
    {
        return Ok(await _offerService.GetListAsync(query, cancellationToken));
    }

    [HttpGet("next-sequence")]
    public async Task<ActionResult<OfferNextSequenceDto>> GetNextSequence(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        var targetYear = year ?? DateTime.UtcNow.Year;
        var targetMonth = month ?? DateTime.UtcNow.Month;
        var userId = CurrentUserId();
        return Ok(await _offerService.GetNextSequenceAsync(targetYear, targetMonth, userId, cancellationToken));
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<List<OfferListItemDto>>> GetVersions(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _offerService.GetVersionsAsync(id, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OfferDetailDto>> GetOffer(
        Guid id,
        CancellationToken cancellationToken)
    {
        var offer = await _offerService.GetByIdAsync(id, cancellationToken);
        return offer == null ? NotFound() : Ok(offer);
    }

    [HttpGet("{id:guid}/document")]
    public async Task<ActionResult<OfferDocumentDto>> GetDocument(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await _offerService.GetDocumentAsync(id, cancellationToken);
        return document == null ? NotFound() : Ok(document);
    }

    [HttpPost]
    public async Task<ActionResult<OfferDetailDto>> CreateOffer(
        [FromBody] OfferDraftDto request,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var offer = await _offerService.CreateAsync(request, userId.Value, cancellationToken);
        return CreatedAtAction(nameof(GetOffer), new { id = offer.Id }, offer);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OfferDetailDto>> UpdateOffer(
        Guid id,
        [FromBody] OfferDraftDto request,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        return Ok(await _offerService.UpdateAsync(id, request, userId.Value, cancellationToken));
    }

    [HttpPost("{id:guid}/revise")]
    public async Task<ActionResult<OfferDetailDto>> ReviseOffer(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var offer = await _offerService.ReviseAsync(id, userId.Value, cancellationToken);
        return CreatedAtAction(nameof(GetOffer), new { id = offer.Id }, offer);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<OfferDetailDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateOfferStatusDto request,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        return Ok(await _offerService.UpdateStatusAsync(id, request, userId.Value, cancellationToken));
    }

    private Guid? CurrentUserId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawId, out var id) ? id : null;
    }
}
