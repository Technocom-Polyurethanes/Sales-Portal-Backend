namespace sales_portal.core.Audit;

/// <summary>Marks an action whose execution must produce an audit row.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class AuditAttribute(string code) : Attribute
{
    public string Code { get; } = code;
}
