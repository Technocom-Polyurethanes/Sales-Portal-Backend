namespace sales_portal.domain.Models
{
    /// <summary>Maximum lengths of the free-text blocks printed on an offer.</summary>
    public static class OfferTextLimits
    {
        public const int DeliveryText = 1000;
        public const int DeliveryPlace = 300;
        public const int PaymentTermsText = 800;
    }
}
