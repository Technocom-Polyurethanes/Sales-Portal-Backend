using sales_portal.core.Calculations;
using sales_portal.core.DTOs;
using sales_portal.core.Services;
using sales_portal.domain.Models;
using Xunit;

namespace SalesPortal.Tests;

public class ReferenceRulesTests
{
    [Fact]
    public void Build_produces_a_valid_reference_for_every_month()
    {
        for (var month = 1; month <= 12; month++)
        {
            var issueDate = new DateOnly(2026, month, 15);
            var reference = ReferenceRules.Build(month, issueDate, "YL", "AA");

            Assert.Equal($"{month}/{ReferenceRules.MonthAbbreviations[month - 1]}/YL/AA", reference);
            Assert.True(ReferenceRules.IsValid(reference), reference);
            Assert.Null(ReferenceRules.Validate(reference, issueDate));
        }
    }

    [Fact]
    public void May_is_the_only_month_without_a_period()
    {
        var issueDate = new DateOnly(2026, 5, 20);

        Assert.True(ReferenceRules.IsValid("77/MAY/YL/AA"));
        Assert.Null(ReferenceRules.Validate("77/MAY/YL/AA", issueDate));
        Assert.False(ReferenceRules.IsValid("77/MAY./YL/AA"));
    }

    [Fact]
    public void Month_matching_is_case_insensitive_and_normalized()
    {
        var issueDate = new DateOnly(2026, 10, 21);

        Assert.True(ReferenceRules.IsValid("3089/oct./YL/aa"));
        Assert.True(ReferenceRules.TryGetMonth("3089/oct./YL/aa", out var month));
        Assert.Equal("OCT.", month);
        Assert.Null(ReferenceRules.Validate("3089/oct./YL/AA", issueDate));
    }

    [Theory]
    [InlineData("3089")]
    [InlineData("3089/OCT/YL/AA")]
    [InlineData("3089/OCT./YL")]
    [InlineData("3089/OCT./YL/AA/EXTRA")]
    [InlineData("OCT./3089/YL/AA")]
    [InlineData("3089/OCT./YL/")]
    [InlineData("3089/OCT./YL/AA BB")]
    public void Invalid_references_are_rejected(string reference)
    {
        Assert.False(ReferenceRules.IsValid(reference));
        Assert.NotNull(ReferenceRules.Validate(reference, new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void Empty_reference_is_not_valid_but_is_left_to_the_required_field_check()
    {
        Assert.False(ReferenceRules.IsValid(string.Empty));
        Assert.Null(ReferenceRules.Validate(string.Empty, new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void Mismatched_month_returns_the_expected_message()
    {
        var error = ReferenceRules.Validate(
            "100/OCT./YL/AA",
            new DateOnly(2026, 11, 5));

        Assert.Equal(
            "Reference month (OCT.) does not match the issue date month (NOV.).",
            error);
    }

    [Fact]
    public void Calculator_reports_a_malformed_reference()
    {
        var calculation = CreateCalculator().Calculate(
            CreateDraft("100/OCT/YL/AA", new DateOnly(2026, 10, 1)),
            CreateCatalog());

        Assert.Contains(
            $"Reference must follow the format {ReferenceRules.ExpectedFormat}.",
            calculation.ValidationErrors);
    }

    [Fact]
    public void Calculator_reports_a_reference_month_that_does_not_match_the_issue_date()
    {
        var calculation = CreateCalculator().Calculate(
            CreateDraft("100/OCT./YL/AA", new DateOnly(2026, 11, 5)),
            CreateCatalog());

        Assert.Contains(
            "Reference month (OCT.) does not match the issue date month (NOV.).",
            calculation.ValidationErrors);
    }

    [Fact]
    public void Calculator_accepts_a_reference_that_matches_the_issue_date()
    {
        var calculation = CreateCalculator().Calculate(
            CreateDraft("100/may/YL/AA", new DateOnly(2026, 5, 1)),
            CreateCatalog());

        Assert.DoesNotContain(calculation.ValidationErrors, error => error.StartsWith("Reference"));
    }

    [Fact]
    public async Task Next_sequence_returns_a_suggested_reference_with_the_requested_month()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        TestFactory.AddOffer(
            db.Context,
            basics,
            "5/MAY/YL/AA",
            version: 5,
            issueDate: new DateOnly(2026, 5, 10));

        var service = TestFactory.CreateOfferService(db.Context);
        var result = await service.GetNextSequenceAsync(2026, 5, basics.User.Id, CancellationToken.None);

        Assert.Equal(6, result.SuggestedSequence);
        Assert.Equal("6/MAY/YL/TU", result.SuggestedReference);
        Assert.Null(ReferenceRules.Validate(
            result.SuggestedReference,
            new DateOnly(2026, 5, 1)));
    }

    private static OfferCalculator CreateCalculator()
        => new(new OfferDocumentBuilder());

    private static OfferDraftDto CreateDraft(string reference, DateOnly issueDate)
        => new()
        {
            Reference = reference,
            IssueDate = issueDate,
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
