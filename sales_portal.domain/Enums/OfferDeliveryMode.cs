namespace sales_portal.domain.Enums
{
    /// <summary>
    /// How the delivery of an offer is described. <see cref="Batches"/> is the dated schedule built
    /// from the ordered packages, <see cref="Text"/> is free wording for conditions a schedule cannot express.
    /// </summary>
    public enum OfferDeliveryMode { Batches, Text }
}
