using sales_portal.core.DTOs;

namespace sales_portal.core.Services.Import;

/// <summary>
/// A preview row as stored on the import batch. The client never sees this shape; it is
/// the server-side memory of the parsed file that the commit trusts.
/// </summary>
internal sealed class StoredImportRow
{
    public int RowNumber { get; set; }
    public string Status { get; set; } = "error";
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, string?> Values { get; set; } = new();
    public Dictionary<string, string?>? Existing { get; set; }
    public List<ImportChangeDto> Changes { get; set; } = new();

    /// <summary>Id of the customer/material this row updates, null for a new record.</summary>
    public Guid? TargetId { get; set; }

    /// <summary>Resolved payment term id (customers only).</summary>
    public Guid? PaymentTermId { get; set; }
}
