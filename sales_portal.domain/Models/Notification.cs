using System;

namespace sales_portal.domain.Models
{
    /// <summary>
    /// One inbox row for one recipient. The actor name and entity label are snapshots so the
    /// message stays readable after the referenced records change or are deleted.
    /// </summary>
    public class Notification
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RecipientUserId { get; set; }
        public string Type { get; set; } = default!;
        public Guid? ActorUserId { get; set; }
        public string ActorName { get; set; } = default!;
        public string EntityType { get; set; } = default!;
        public Guid? EntityId { get; set; }
        public string EntityLabel { get; set; } = default!;
        public string Meta { get; set; } = "{}";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ReadAt { get; set; }
    }
}
