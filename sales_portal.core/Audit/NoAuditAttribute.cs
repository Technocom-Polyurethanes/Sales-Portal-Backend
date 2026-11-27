namespace sales_portal.core.Audit;

/// <summary>
/// Marks a POST that only calculates or changes personal read state, so the coverage test can
/// tell it apart from a forgotten audit annotation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class NoAuditAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
