namespace sales_portal.core.DTOs;

public class SystemStatusDto
{
    public string Status { get; set; } = "Healthy";
    public DateTimeOffset CheckedAt { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public long UptimeSeconds { get; set; }
    public SystemDatabaseStatusDto Database { get; set; } = new();
    public SystemLiveStatusDto Live { get; set; } = new();
}

public class SystemDatabaseStatusDto
{
    public string Status { get; set; } = "Down";
    public long LatencyMs { get; set; }
}

public class SystemLiveStatusDto
{
    public string State { get; set; } = "disconnected";
    public bool Disabled { get; set; }
    public bool PollingFallback { get; set; }
    public string Changes { get; set; } = "disconnected";
    public string Notifications { get; set; } = "disconnected";
}
