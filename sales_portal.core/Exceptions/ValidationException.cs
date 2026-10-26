namespace sales_portal.core.Exceptions;

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
