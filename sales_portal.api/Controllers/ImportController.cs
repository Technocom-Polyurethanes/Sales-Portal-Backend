using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.IService;

namespace sales_portal.api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;

    public ImportController(IImportService importService)
    {
        _importService = importService;
    }

    [HttpGet("templates/{type}")]
    public async Task<IActionResult> GetTemplate(
        string type,
        CancellationToken cancellationToken)
    {
        var template = await _importService.GetTemplateAsync(type, cancellationToken);
        return File(template.Content, template.ContentType, template.FileName);
    }

    [HttpPost("{type}/preview")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<ImportPreviewDto>> Preview(
        string type,
        CancellationToken cancellationToken)
    {
        var file = Request.Form.Files.Count > 0 ? Request.Form.Files[0] : null;
        if (file == null || file.Length == 0)
        {
            throw new ValidationException("Choose a file to import.");
        }

        await using var content = file.OpenReadStream();
        return Ok(await _importService.PreviewAsync(
            type,
            content,
            file.FileName,
            file.Length,
            CurrentUserId(),
            cancellationToken));
    }

    [HttpPost("{importId:guid}/commit")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ImportCommitResultDto>> Commit(
        Guid importId,
        CancellationToken cancellationToken)
    {
        return Ok(await _importService.CommitAsync(importId, cancellationToken));
    }

    [HttpGet("history")]
    public async Task<ActionResult<PaginatedResultDto<ImportHistoryItemDto>>> History(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _importService.GetHistoryAsync(page, pageSize, cancellationToken));
    }

    private Guid? CurrentUserId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawId, out var id) ? id : null;
    }
}
