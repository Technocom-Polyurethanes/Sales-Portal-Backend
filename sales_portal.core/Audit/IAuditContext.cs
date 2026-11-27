namespace sales_portal.core.Audit;

/// <summary>Per-request enrichment filled by services next to the code that emits notifications.</summary>
public interface IAuditContext
{
    string? EntityType { get; }
    Guid? EntityId { get; }
    string? EntityLabel { get; }
    string? Description { get; }
    IReadOnlyDictionary<string, string> Details { get; }

    void SetEntity(string type, Guid? id, string? label);

    void SetDescription(string description);

    void AddDetail(string key, string value);
}

public sealed class AuditContext : IAuditContext
{
    private readonly Dictionary<string, string> _details = new(StringComparer.Ordinal);

    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public string? EntityLabel { get; private set; }
    public string? Description { get; private set; }
    public IReadOnlyDictionary<string, string> Details => _details;

    public void SetEntity(string type, Guid? id, string? label)
    {
        EntityType = type;
        EntityId = id;
        EntityLabel = label;
    }

    public void SetDescription(string description) => Description = description;

    public void AddDetail(string key, string value) => _details[key] = value;
}

/// <summary>Used when a service is constructed outside an HTTP request (tests, jobs).</summary>
public sealed class NoAuditContext : IAuditContext
{
    public static readonly NoAuditContext Instance = new();

    private NoAuditContext() { }

    public string? EntityType => null;
    public Guid? EntityId => null;
    public string? EntityLabel => null;
    public string? Description => null;
    public IReadOnlyDictionary<string, string> Details => new Dictionary<string, string>();

    public void SetEntity(string type, Guid? id, string? label) { }
    public void SetDescription(string description) { }
    public void AddDetail(string key, string value) { }
}
