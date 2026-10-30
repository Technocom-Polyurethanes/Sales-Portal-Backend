namespace sales_portal.core.DTOs;

/// <summary>
/// Suggestion for the next offer reference. The reference is still typed manually by
/// the user; this only proposes the next sequence for the requested month.
/// </summary>
public class OfferNextSequenceDto
{
    public int SuggestedSequence { get; set; }
    public string SuggestedReference { get; set; } = string.Empty;
}
