using System;

namespace sales_portal.domain.Models
{
    /// <summary>
    /// One uploaded file: the parsed preview rows live here until the commit, and the
    /// same record is the history entry once committed.
    /// </summary>
    public class ImportBatch : BaseEntity
    {
        public string Type { get; set; } = default!;          // customers | materials | prices
        public string FileName { get; set; } = default!;
        public string Status { get; set; } = "Pending";        // Pending | Committed
        public Guid? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public string RowsJson { get; set; } = "[]";
        public DateTimeOffset? CommittedAt { get; set; }
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
    }
}
