using sales_portal.core.Calculations;
using sales_portal.core.DTOs;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using Xunit;

namespace SalesPortal.Tests;

public class ReferenceRulesTests
{
    [Fact]
    public void Build_produces_the_type_sequence_and_initials()
    {
        Assert.Equal("Loc/42/YL/JD", ReferenceRules.Build(OfferType.Loc, 42, "jd"));
        Assert.Equal("Int/43/YL/AB", ReferenceRules.Build(OfferType.Int, 43, "AB"));
        Assert.True(ReferenceRules.IsValid("Loc/42/YL/JD"));
        Assert.True(ReferenceRules.IsValid("Int/43/YL/AB"));
        Assert.Null(ReferenceRules.Validate("Loc/42/YL/JD", OfferType.Loc));
        Assert.Null(ReferenceRules.Validate("Int/43/YL/AB", OfferType.Int));
    }

    [Fact]
    public void Reference_type_must_match_the_offer_type()
    {
        Assert.Equal(
            "Reference must start with 'Loc' to match the offer type.",
            ReferenceRules.Validate("Int/43/YL/AB", OfferType.Loc));
        Assert.Equal(
            "Reference must start with 'Int' to match the offer type.",
            ReferenceRules.Validate("Loc/42/YL/JD", OfferType.Int));
    }

    [Theory]
    [InlineData("42/YL/JD")]
    [InlineData("Loc/42/AA/JD")]
    [InlineData("Loc/42/YL")]
    [InlineData("Loc/42/YL/JD/EXTRA")]
    [InlineData("Loc//YL/JD")]
    [InlineData("Loc/42/YL/JD BB")]
    [InlineData("X/42/YL/JD")]
    public void Invalid_references_are_rejected(string reference)
    {
        Assert.False(ReferenceRules.IsValid(reference));
        Assert.NotNull(ReferenceRules.Validate(reference, OfferType.Loc));
    }

    [Fact]
    public void Missing_offer_type_is_rejected_for_a_typed_reference()
    {
        Assert.Equal("Offer type is required.", ReferenceRules.Validate("Loc/42/YL/JD", null));
    }

    [Fact]
    public void Empty_reference_is_left_to_the_required_field_check()
    {
        Assert.False(ReferenceRules.IsValid(string.Empty));
        Assert.Null(ReferenceRules.Validate(string.Empty, OfferType.Loc));
    }

    [Fact]
    public void Legacy_references_still_yield_their_sequence()
    {
        Assert.True(ReferenceRules.TryGetSequence("3089/OCT./YL/AA", out var legacy));
        Assert.Equal(3089, legacy);
        Assert.True(ReferenceRules.TryGetSequence("Loc/42/YL/JD", out var current));
        Assert.Equal(42, current);
        Assert.False(ReferenceRules.TryGetSequence("not-a-reference", out _));
    }

    [Fact]
    public void Initials_come_from_the_user_and_fall_back_to_the_full_name()
    {
        Assert.Equal("JD", ReferenceRules.InitialsFrom("jd", "John Doe"));
        Assert.Equal("JD", ReferenceRules.InitialsFrom(" j.d. ", "John Doe"));
        Assert.Equal("JD", ReferenceRules.InitialsFrom(null, "John Doe"));
        Assert.Equal("NA", ReferenceRules.InitialsFrom("  ", null));
    }

    [Fact]
    public void Calculator_reports_a_malformed_reference()
    {
        var calculation = CreateCalculator().Calculate(
            CreateDraft("Loc/42/AA/JD"),
            CreateCatalog());

        Assert.Contains(
            $"Reference must follow the format {ReferenceRules.ExpectedFormat}.",
            calculation.ValidationErrors);
    }

    [Fact]
    public void Calculator_reports_a_reference_type_that_does_not_match()
    {
        var calculation = CreateCalculator().Calculate(
            CreateDraft("Int/42/YL/JD", OfferType.Loc),
            CreateCatalog());

        Assert.Contains(
            "Reference must start with 'Loc' to match the offer type.",
            calculation.ValidationErrors);
    }

    [Fact]
    public void Calculator_reports_a_missing_offer_type()
    {
        var calculation = CreateCalculator().Calculate(
            CreateDraft("Loc/42/YL/JD", offerType: null),
            CreateCatalog());

        Assert.Contains("Offer type is required.", calculation.ValidationErrors);
    }

    [Fact]
    public void Calculator_accepts_a_matching_reference()
    {
        var calculation = CreateCalculator().Calculate(
            CreateDraft("Int/42/YL/JD", OfferType.Int),
            CreateCatalog());

        Assert.DoesNotContain(calculation.ValidationErrors, error => error.StartsWith("Reference"));
    }

    [Fact]
    public async Task Next_sequence_starts_at_one_and_includes_the_user_initials()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var service = TestFactory.CreateOfferService(db.Context);

        var result = await service.GetNextSequenceAsync(OfferType.Loc, basics.User.Id, CancellationToken.None);

        Assert.Equal(1, result.SuggestedSequence);
        Assert.Equal("Loc/1/YL/TU", result.SuggestedReference);
    }

    [Fact]
    public async Task Next_sequence_increments_from_the_last_recorded_offer()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        TestFactory.AddOffer(db.Context, basics, "Loc/41/YL/AA", serialNumber: 41);
        TestFactory.AddOffer(db.Context, basics, "Int/42/YL/AA", serialNumber: 42);

        var service = TestFactory.CreateOfferService(db.Context);

        var local = await service.GetNextSequenceAsync(OfferType.Loc, basics.User.Id, CancellationToken.None);
        var international = await service.GetNextSequenceAsync(OfferType.Int, basics.User.Id, CancellationToken.None);

        Assert.Equal(43, local.SuggestedSequence);
        Assert.Equal("Loc/43/YL/TU", local.SuggestedReference);
        Assert.Equal("Int/43/YL/TU", international.SuggestedReference);
    }

    [Fact]
    public async Task Next_sequence_falls_back_to_a_legacy_first_segment()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        TestFactory.AddOffer(db.Context, basics, "77/OCT./YL/AA", serialNumber: 0);

        var service = TestFactory.CreateOfferService(db.Context);
        var result = await service.GetNextSequenceAsync(OfferType.Int, basics.User.Id, CancellationToken.None);

        Assert.Equal(78, result.SuggestedSequence);
        Assert.Equal("Int/78/YL/TU", result.SuggestedReference);
    }

    private static OfferCalculator CreateCalculator()
        => new(new OfferDocumentBuilder());

    private static OfferDraftDto CreateDraft(string reference, OfferType? offerType = OfferType.Loc)
        => new()
        {
            Reference = reference,
            OfferType = offerType,
            IssueDate = new DateOnly(2026, 10, 1),
            CustomerId = Guid.NewGuid()
        };

    private static OfferCatalog CreateCatalog()
        => new()
        {
            Customer = new Customer
            {
                Name = "Test Customer",
                Address = "Test address"
            },
            PaymentTerm = new PaymentTerm
            {
                Code = "transfer-order",
                Label = "Bank Transfer",
                LabelAr = "تحويل بنكي",
                Days = 0
            },
            CompanySettings = new CompanySettings
            {
                Name = "TECHNOCOM",
                ReferencePrefix = "YL"
            }
        };
}
