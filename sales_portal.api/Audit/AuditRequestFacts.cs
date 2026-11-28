namespace sales_portal.api.Audit;

/// <summary>Request-only facts the filter knows but the action result and claims do not.</summary>
public sealed record AuditRequestFacts(
    string? UserAgent = null,
    int? DurationMs = null,
    string? AttemptedUsername = null,
    AuditResponseActor? ResponseActor = null);

/// <summary>Identity from a server-generated login response, never from request input.</summary>
public sealed record AuditResponseActor(Guid Id, string Username, string FullName, string Role);
