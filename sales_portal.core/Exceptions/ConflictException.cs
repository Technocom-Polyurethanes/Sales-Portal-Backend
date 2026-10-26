namespace sales_portal.core.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
