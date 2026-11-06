namespace sales_portal.core.DTOs;

/// <summary>
/// Suggestion for the next offer reference: the next global sequence and the reference
/// built from it. <see cref="IsFirstOffer"/> is true while no offer exists yet, which is
/// the only time the user may choose the starting number.
/// </summary>
public class OfferNextSequenceDto
{
    public int SuggestedSequence { get; set; }
    public string SuggestedReference { get; set; } = string.Empty;
    public bool IsFirstOffer { get; set; }
}
