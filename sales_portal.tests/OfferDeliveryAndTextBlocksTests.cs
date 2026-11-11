using sales_portal.core.DTOs;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using Xunit;

namespace SalesPortal.Tests;

// Delivery is either a dated batch schedule or free text, and an offer also carries a delivery place,
// extra payment conditions and the fixed terms and conditions.
public class OfferDeliveryAndTextBlocksTests
{
    [Fact]
    public void Text_delivery_keeps_the_text_and_drops_scheduled_batches()
    {
        var draft = Draft(OfferDeliveryMode.Text);
        draft.DeliveryText = "  Delivered within 3 days of the purchase order.  ";
        draft.DeliveryBatches.Add(Batch());

        var result = Calculate(draft);

        Assert.Empty(result.ValidationErrors);
        Assert.Empty(result.Draft.DeliveryBatches);
        Assert.Empty(result.DeliveryBatches);
        Assert.Equal("Delivered within 3 days of the purchase order.", result.Document.DeliveryText);
        Assert.Equal(OfferDeliveryMode.Text, result.Document.DeliveryMode);
    }

    [Fact]
    public void Batch_delivery_drops_any_leftover_delivery_text()
    {
        var draft = Draft(OfferDeliveryMode.Batches);
        draft.DeliveryText = "Left over from a previous mode.";

        var result = Calculate(draft);

        Assert.Null(result.Draft.DeliveryText);
        Assert.Null(result.Document.DeliveryText);
        Assert.Equal(OfferDeliveryMode.Batches, result.Document.DeliveryMode);
    }

    [Fact]
    public void Blank_text_blocks_become_null_and_filled_ones_are_trimmed()
    {
        var draft = Draft(OfferDeliveryMode.Batches);
        draft.DeliveryPlace = "   ";
        draft.PaymentTermsText = "  Bank transfer on delivery.\nTax deduction notice attached.  ";

        var result = Calculate(draft);

        Assert.Null(result.Document.DeliveryPlace);
        Assert.Equal("Bank transfer on delivery.\nTax deduction notice attached.", result.Document.PaymentTermsText);
    }

    [Fact]
    public void Text_blocks_longer_than_their_limit_are_rejected()
    {
        var draft = Draft(OfferDeliveryMode.Text);
        draft.DeliveryText = new string('a', OfferTextLimits.DeliveryText + 1);
        draft.DeliveryPlace = new string('b', OfferTextLimits.DeliveryPlace + 1);
        draft.PaymentTermsText = new string('c', OfferTextLimits.PaymentTermsText + 1);

        var result = Calculate(draft);

        Assert.Contains(result.ValidationErrors, error => error.StartsWith("Delivery text"));
        Assert.Contains(result.ValidationErrors, error => error.StartsWith("Delivery place"));
        Assert.Contains(result.ValidationErrors, error => error.StartsWith("Payment terms text"));
    }

    [Theory]
    [InlineData(DocumentLanguage.En, "Force majeure")]
    [InlineData(DocumentLanguage.Ar, "القوى القهرية")]
    public void Every_offer_prints_the_fixed_terms_in_its_language(DocumentLanguage language, string expectedFragment)
    {
        var draft = Draft(OfferDeliveryMode.Batches);
        draft.DocumentLanguage = language;

        var result = Calculate(draft);

        Assert.Equal(2, result.Document.TermsAndConditions.Count);
        Assert.Contains(result.Document.TermsAndConditions, term => term.Contains(expectedFragment));
    }

    private static OfferCalculateResponseDto Calculate(OfferDraftDto draft)
        => new OfferCalculator(new OfferDocumentBuilder()).Calculate(draft, Catalog());

    private static OfferDraftDto Draft(OfferDeliveryMode mode)
        => new()
        {
            Reference = "Loc/100/YL/AA",
            OfferType = OfferType.Loc,
            IssueDate = new DateOnly(2026, 11, 8),
            CustomerId = Guid.NewGuid(),
            PaymentTermId = Guid.NewGuid(),
            DeliveryMode = mode
        };

    private static OfferDraftDeliveryBatchDto Batch()
        => new()
        {
            Id = Guid.NewGuid(),
            BatchNumber = 1,
            DeliveryDate = new DateOnly(2026, 11, 20)
        };

    private static OfferCatalog Catalog()
    {
        var term = new PaymentTerm { Code = "transfer-order", Label = "Bank Transfer", LabelAr = "تحويل بنكي", Days = 0 };
        return new OfferCatalog
        {
            Customer = new Customer { Name = "Test Customer", Address = "Test address" },
            PaymentTerm = term,
            PaymentTerms = [term],
            Materials = [],
            PackageTypes = [],
            CompanySettings = new CompanySettings { Name = "TECHNOCOM", ReferencePrefix = "YL" }
        };
    }
}
