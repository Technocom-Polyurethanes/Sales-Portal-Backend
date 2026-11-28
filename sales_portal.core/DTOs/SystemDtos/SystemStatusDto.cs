namespace sales_portal.core.DTOs;

public class SystemStatusDto
{
    public string Status { get; set; } = "Healthy";
    public DateTimeOffset CheckedAt { get; set; }
    public string Version { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public string? Commit { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public string Environment { get; set; } = string.Empty;
    public long UptimeSeconds { get; set; }
    public SystemDatabaseStatusDto Database { get; set; } = new();
    public SystemLiveStatusDto Live { get; set; } = new();
    public SystemRealtimeStatusDto Realtime { get; set; } = new();
    public SystemAuditStatusDto Audit { get; set; } = new();
    public List<SystemJobDto> Jobs { get; set; } = [];
    public SystemSecurityStatusDto Security { get; set; } = new();
    public SystemDataStatusDto Data { get; set; } = new();
}

public class SystemDatabaseStatusDto
{
    public string Status { get; set; } = "Down";
    public long LatencyMs { get; set; }
    public long[] LatencyHistory { get; set; } = [];
    public string? ServerVersion { get; set; }
    public long? SizeBytes { get; set; }
    public int? ConnectionsUsed { get; set; }
    public int? ConnectionsMax { get; set; }
    public int? PendingMigrations { get; set; }
    public string? LastMigration { get; set; }
}

public class SystemLiveStatusDto
{
    public string State { get; set; } = "disconnected";
    public bool Disabled { get; set; }
    public bool PollingFallback { get; set; }
    public string Changes { get; set; } = "disconnected";
    public string Notifications { get; set; } = "disconnected";
    public DateTimeOffset? ChangesLastEventAt { get; set; }
    public DateTimeOffset? NotificationsLastEventAt { get; set; }
    public int ReconnectCount { get; set; }
    public string? LastError { get; set; }
}

public class SystemRealtimeStatusDto
{
    public int ConnectedClients { get; set; }
    public int NotificationQueueDepth { get; set; }
    public int Dropped { get; set; }
}

public class SystemAuditStatusDto
{
    public long? RowsTotal { get; set; }
    public int QueueDepth { get; set; }
    public int Dropped { get; set; }
    public DateTimeOffset? LastWriteAt { get; set; }
    public int? RetentionDays { get; set; }
}

public class SystemJobDto
{
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset? LastRunAt { get; set; }
    public long? DurationMs { get; set; }
    public string? Result { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
}

public class SystemSecurityStatusDto
{
    public int? FailedSignIns24h { get; set; }
    public DateTimeOffset? LastFailedSignInAt { get; set; }
    public int? ActiveUsers { get; set; }
    public int? InactiveUsers { get; set; }
}

public class SystemDataStatusDto
{
    public int? Offers { get; set; }
    public int? Customers { get; set; }
    public int? Materials { get; set; }
    public int? ActiveUsers { get; set; }
}

public sealed record SystemProbeDto(string Name, bool Ok, int Count, string? Error);
