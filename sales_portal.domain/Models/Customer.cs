using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sales_portal.domain.Models
{
    public class Customer : BaseEntity
    {
        public string CustomerCode { get; set; } = default!;      // unique
        public string? ErpNextId { get; set; }
        public string Name { get; set; } = default!;              // English name
        public string? NameAr { get; set; }
        public string? TaxId { get; set; }                        // prefills the offer's Bill-To tax id
        public string? ContactPerson { get; set; }                 // prefills "Attention"
        public string? ContactPersonAr { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? CityAr { get; set; }
        public string Address { get; set; } = default!;           // Bill To / Ship To
        public string? AddressAr { get; set; }
        public Guid? DefaultPaymentTermId { get; set; }
        public PaymentTerm? DefaultPaymentTerm { get; set; }
        public string DefaultCurrency { get; set; } = "EGP";

        // OffersCount, TotalValue, LastOfferDate are computed from Offers (not stored).
        public ICollection<Offer> Offers { get; set; } = new List<Offer>();
    }
}
