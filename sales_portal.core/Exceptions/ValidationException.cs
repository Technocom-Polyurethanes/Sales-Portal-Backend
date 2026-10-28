namespace sales_portal.core.Exceptions;

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }

    public ValidationException(string message, IDictionary<string, string[]> fieldErrors) : base(message)
    {
        FieldErrors = fieldErrors;
    }

    /// <summary>
    /// Optional field -> messages map, rendered as the <c>errors</c> object of a
    /// <c>ValidationProblemDetails</c> response.
    /// </summary>
    public IDictionary<string, string[]>? FieldErrors { get; }
}
