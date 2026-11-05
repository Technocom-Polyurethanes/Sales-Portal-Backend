namespace sales_portal.core.DTOs;

/// <summary>
/// Suggestion for the next offer reference. The reference is still typed manually by
/// the user; this proposes the next global sequence for the selected offer type.
/// </summary>
public class OfferNextSequenceDto
{
    public int SuggestedSequence { get; set; }
    public string SuggestedReference { get; set; } = string.Empty;
}
