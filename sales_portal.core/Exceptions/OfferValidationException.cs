namespace sales_portal.core.Exceptions;

public class OfferValidationException : ValidationException
{
    public OfferValidationException(IEnumerable<string> errors, IEnumerable<string> warnings)
        : base("Offer validation failed.")
    {
        Errors = errors.ToList();
        Warnings = warnings.ToList();
    }

    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }
}
