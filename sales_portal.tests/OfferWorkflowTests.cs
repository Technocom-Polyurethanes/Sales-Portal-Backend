using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using sales_portal.core.DTOs;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;
using Xunit;

namespace SalesPortal.Tests;

// The frontend saves an offer with POST /api/offers (always a Draft) followed by
// PATCH /api/offers/{id}/status. These tests drive that exact contract over HTTP.
[Collection(ProcessEnvironment.Name)]
public class OfferWorkflowTests : IClassFixture<SalesPortalApiFactory>
{
    private static int _sequence = 1000;

    private readonly SalesPortalApiFactory _factory;

    public OfferWorkflowTests(SalesPortalApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_with_the_frontend_payload_binds_and_calculates()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedClient(seed.User.Id);
        var payload = BuildPayload(seed, NextReference(), out var lineId, out var packagingId);

        var response = await client.PostAsJsonAsync("/api/offers", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var offer = await ReadJsonAsync(response);
        Assert.Equal("Draft", offer.GetProperty("status").GetString());
        // dueDate comes from the payment term (cheque-30), the discount from the percent.
        Assert.Equal("2026-11-20", offer.GetProperty("dueDate").GetString());
        Assert.Equal(100000m, offer.GetProperty("subtotal").GetDecimal());
        Assert.Equal(10000m, offer.GetProperty("orderDiscount").GetDecimal());
        Assert.Equal(10m, offer.GetProperty("orderDiscountPercent").GetDecimal());

        // Allocations are re-linked to the ids that were stored, not the ones the client sent.
        var storedLine = offer.GetProperty("lines")[0];
        var storedPackaging = storedLine.GetProperty("packaging")[0];
        var allocation = offer.GetProperty("deliveryBatches")[0].GetProperty("allocations")[0];
        Assert.Equal(storedLine.GetProperty("id").GetGuid(), allocation.GetProperty("offerLineId").GetGuid());
        Assert.Equal(storedPackaging.GetProperty("id").GetGuid(), allocation.GetProperty("offerLinePackagingId").GetGuid());
        Assert.NotEqual(lineId, storedLine.GetProperty("id").GetGuid());
        Assert.NotEqual(packagingId, storedPackaging.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Post_always_creates_a_draft_even_when_the_body_says_Issued()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedClient(seed.User.Id);
        var payload = BuildPayload(seed, NextReference());
        payload["status"] = "Issued";

        var response = await client.PostAsJsonAsync("/api/offers", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Draft", (await ReadJsonAsync(response)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Patch_status_moves_a_draft_to_Issued_case_insensitively()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedClient(seed.User.Id);
        var id = await CreateDraftAsync(client, BuildPayload(seed, NextReference()));

        var response = await PatchStatusAsync(client, id, "issued");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Issued", (await ReadJsonAsync(response)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Patch_status_rejects_an_invalid_draft_and_leaves_it_a_draft()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedClient(seed.User.Id);
        Guid id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // No lines: issuing must fail on that alone.
            id = TestFactory.AddOffer(
                db,
                new SeededBasics(seed.User, seed.User, seed.Customer, seed.PaymentTerm),
                NextReference(),
                issueDate: new DateOnly(2026, 11, 5)).Id;
        }

        var response = await PatchStatusAsync(client, id, "Issued");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadJsonAsync(response);
        var detail = problem.GetProperty("detail").GetString();
        Assert.Contains("At least one line is required", detail);
        Assert.Equal(JsonValueKind.Array, problem.GetProperty("errors").ValueKind);

        var current = await client.GetAsync($"/api/offers/{id}");
        Assert.Equal("Draft", (await ReadJsonAsync(current)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task New_version_with_a_changed_reference_stays_in_the_same_offer_chain()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedClient(seed.User.Id);
        var firstId = await CreateDraftAsync(client, BuildPayload(seed, NextReference()));
        var first = await ReadJsonAsync(await client.GetAsync($"/api/offers/{firstId}"));

        // What the frontend sends when an offer is edited: a new reference, the parent and a stale version.
        var revisedPayload = BuildPayload(seed, NextReference());
        revisedPayload["parentOfferId"] = firstId;
        var response = await client.PostAsJsonAsync("/api/offers", revisedPayload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var revised = await ReadJsonAsync(response);

        Assert.Equal(2, revised.GetProperty("version").GetInt32());
        Assert.Equal(
            first.GetProperty("rootOfferId").GetGuid(),
            revised.GetProperty("rootOfferId").GetGuid());
        Assert.Equal(2, revised.GetProperty("versionsCount").GetInt32());

        var versions = await ReadJsonAsync(
            await client.GetAsync($"/api/offers/{revised.GetProperty("id").GetGuid()}/versions"));
        Assert.Equal(2, versions.GetArrayLength());
    }

    [Fact]
    public async Task A_new_offer_cannot_take_the_reference_of_another_offer()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedClient(seed.User.Id);
        var taken = NextReference();
        await CreateDraftAsync(client, BuildPayload(seed, taken));
        var otherId = await CreateDraftAsync(client, BuildPayload(seed, NextReference()));

        var payload = BuildPayload(seed, taken);
        payload["parentOfferId"] = otherId;
        var response = await client.PostAsJsonAsync("/api/offers", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Validity_days_round_trip_and_reach_the_document()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedClient(seed.User.Id);
        var payload = BuildPayload(seed, NextReference());
        payload["validityDays"] = 14;

        var id = await CreateDraftAsync(client, payload);
        var detail = await ReadJsonAsync(await client.GetAsync($"/api/offers/{id}"));

        Assert.Equal(14, detail.GetProperty("validityDays").GetInt32());
        Assert.Equal("نادر وليام", detail.GetProperty("attentionAr").GetString());
        var metadata = detail.GetProperty("document").GetProperty("metadata");
        Assert.Equal(14, metadata.GetProperty("validityDays").GetInt32());
        Assert.Equal("2026-11-04", metadata.GetProperty("expiryDate").GetString());
    }

    [Fact]
    public async Task Duplicate_reference_and_version_returns_409_with_a_readable_detail()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedClient(seed.User.Id);
        var reference = NextReference();
        await CreateDraftAsync(client, BuildPayload(seed, reference));

        var response = await client.PostAsJsonAsync("/api/offers", BuildPayload(seed, reference));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("already exists", (await ReadJsonAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Patch_status_rejects_Issued_to_Draft()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedClient(seed.User.Id);
        var id = await CreateDraftAsync(client, BuildPayload(seed, NextReference()));
        Assert.Equal(HttpStatusCode.OK, (await PatchStatusAsync(client, id, "Issued")).StatusCode);

        var response = await PatchStatusAsync(client, id, "Draft");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "Cannot transition an offer from Issued to Draft.",
            (await ReadJsonAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Invalid_guid_in_the_body_returns_a_readable_problem_details_response()
    {
        var seed = await SeedAsync();
        using var client = _factory.CreateAuthenticatedClient(seed.User.Id);
        var payload = BuildPayload(seed, NextReference());
        payload["customerId"] = "not-a-guid";

        var response = await client.PostAsJsonAsync("/api/offers", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadJsonAsync(response);
        var detail = problem.GetProperty("detail").GetString();
        Assert.Contains("customerId", detail);
        Assert.DoesNotContain("LineNumber", detail);
        Assert.Equal(detail, problem.GetProperty("message").GetString());
    }

    private async Task<Seed> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new User
        {
            Username = $"user-{Guid.NewGuid():N}",
            FullName = "Api Tester",
            Initials = "TU",
            PasswordHash = "not-used",
            Role = UserRole.Admin,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Customer, payment terms, materials and package types come from DbInitializer.
        return new Seed(
            user,
            await db.Customers.AsNoTracking().FirstAsync(c => c.CustomerCode == "CUST-DURAVIT"),
            await db.PaymentTerms.AsNoTracking().FirstAsync(t => t.Code == "cheque-30"),
            await db.Materials.AsNoTracking().FirstAsync(m => m.ItemCode == "50001"),
            await db.PackageTypes.AsNoTracking().FirstAsync(p => p.Name == "Drum"));
    }

    private static string NextReference() => $"Loc/{Interlocked.Increment(ref _sequence)}/YL/TU";

    private static Dictionary<string, object?> BuildPayload(Seed seed, string reference)
        => BuildPayload(seed, reference, out _, out _);

    // Mirrors what the frontend sends: no dueDate, no flat orderDiscount, no status, and
    // allocations without an id.
    private static Dictionary<string, object?> BuildPayload(
        Seed seed,
        string reference,
        out Guid lineId,
        out Guid packagingId)
    {
        lineId = Guid.NewGuid();
        packagingId = Guid.NewGuid();

        return new Dictionary<string, object?>
        {
            ["reference"] = reference,
            ["offerType"] = "Loc",
            ["version"] = 1,
            ["customerId"] = seed.Customer.Id,
            ["attention"] = "Nader William",
            ["attentionAr"] = "نادر وليام",
            ["taxId"] = "200-145-890",
            ["issueDate"] = "2026-10-21",
            ["paymentTermId"] = seed.PaymentTerm.Id,
            ["currency"] = "EGP",
            ["documentLanguage"] = "En",
            ["notes"] = "Two-call save",
            ["orderDiscountPercent"] = 10,
            ["taxPercent"] = 14,
            ["otherTaxEnabled"] = false,
            ["otherTaxPercent"] = 0,
            ["lines"] = new[]
            {
                new
                {
                    id = lineId,
                    sortOrder = 1,
                    materialId = seed.Material.Id,
                    ratePerKg = 100m,
                    discountPercent = 0,
                    packaging = new[]
                    {
                        new
                        {
                            id = packagingId,
                            sortOrder = 1,
                            packageTypeId = seed.Drum.Id,
                            weightPerPackageKg = 200m,
                            packages = 5
                        }
                    }
                }
            },
            ["deliveryBatches"] = new[]
            {
                new
                {
                    id = Guid.NewGuid(),
                    batchNumber = 1,
                    deliveryDate = "2026-11-01",
                    allocations = new[]
                    {
                        new
                        {
                            offerLineId = lineId,
                            offerLinePackagingId = packagingId,
                            materialId = seed.Material.Id,
                            packageTypeId = seed.Drum.Id,
                            packages = 5
                        }
                    }
                }
            }
        };
    }

    private static async Task<Guid> CreateDraftAsync(HttpClient client, Dictionary<string, object?> payload)
    {
        var response = await client.PostAsJsonAsync("/api/offers", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> PatchStatusAsync(HttpClient client, Guid id, string status)
        => client.PatchAsync(
            $"/api/offers/{id}/status",
            new StringContent($"{{\"status\":\"{status}\"}}", Encoding.UTF8, "application/json"));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private sealed record Seed(
        User User,
        Customer Customer,
        PaymentTerm PaymentTerm,
        Material Material,
        PackageType Drum);
}

public class OfferIdRelinkTests
{
    [Fact]
    public void Empty_ids_are_replaced_and_allocations_follow_by_material_and_package_type()
    {
        var setup = CreateSetup();
        var draft = setup.Draft(
            (Guid.Empty, setup.MaterialA, [(Guid.Empty, 5)]),
            (Guid.Empty, setup.MaterialB, [(Guid.Empty, 5)]));
        draft.DeliveryBatches.Add(Batch(Allocation(Guid.Empty, Guid.Empty, setup.MaterialB, setup.Drum, 2)));

        var result = new OfferCalculator(new OfferDocumentBuilder()).Calculate(draft, setup.Catalog);

        Assert.Empty(result.ValidationErrors);
        var lineB = result.Lines.Single(line => line.MaterialId == setup.MaterialB.Id);
        Assert.NotEqual(Guid.Empty, lineB.Id);
        var allocation = Assert.Single(Assert.Single(result.DeliveryBatches).Allocations);
        Assert.Equal(lineB.Id, allocation.OfferLineId);
        Assert.Equal(lineB.Packaging[0].Id, allocation.OfferLinePackagingId);
    }

    [Fact]
    public void Duplicate_packaging_ids_on_two_lines_keep_each_allocation_on_its_own_line()
    {
        var setup = CreateSetup();
        var lineA = Guid.NewGuid();
        var lineB = Guid.NewGuid();
        var sharedPackagingId = Guid.NewGuid();
        var draft = setup.Draft(
            (lineA, setup.MaterialA, [(sharedPackagingId, 5)]),
            (lineB, setup.MaterialB, [(sharedPackagingId, 5)]));
        draft.DeliveryBatches.Add(Batch(Allocation(lineB, sharedPackagingId, setup.MaterialB, setup.Drum, 2)));

        var result = new OfferCalculator(new OfferDocumentBuilder()).Calculate(draft, setup.Catalog);

        Assert.Empty(result.ValidationErrors);
        Assert.DoesNotContain(result.ValidationWarnings, warning => warning.Contains("corrected"));
        var finalLineB = result.Lines.Single(line => line.MaterialId == setup.MaterialB.Id);
        var allocation = Assert.Single(Assert.Single(result.DeliveryBatches).Allocations);
        Assert.Equal(finalLineB.Id, allocation.OfferLineId);
        Assert.Equal(finalLineB.Packaging[0].Id, allocation.OfferLinePackagingId);
        Assert.NotEqual(sharedPackagingId, allocation.OfferLinePackagingId);
    }

    [Fact]
    public void Duplicate_line_ids_keep_each_allocation_on_its_own_line()
    {
        var setup = CreateSetup();
        var sharedLineId = Guid.NewGuid();
        var packagingA = Guid.NewGuid();
        var packagingB = Guid.NewGuid();
        var draft = setup.Draft(
            (sharedLineId, setup.MaterialA, [(packagingA, 5)]),
            (sharedLineId, setup.MaterialB, [(packagingB, 5)]));
        draft.DeliveryBatches.Add(Batch(Allocation(sharedLineId, packagingB, setup.MaterialB, setup.Drum, 2)));

        var result = new OfferCalculator(new OfferDocumentBuilder()).Calculate(draft, setup.Catalog);

        Assert.Empty(result.ValidationErrors);
        Assert.DoesNotContain(result.ValidationWarnings, warning => warning.Contains("corrected"));
        var finalLineB = result.Lines.Single(line => line.MaterialId == setup.MaterialB.Id);
        Assert.NotEqual(sharedLineId, finalLineB.Id);
        var allocation = Assert.Single(Assert.Single(result.DeliveryBatches).Allocations);
        Assert.Equal(finalLineB.Id, allocation.OfferLineId);
        Assert.Equal(packagingB, allocation.OfferLinePackagingId);
    }

    private static OfferDraftDeliveryBatchDto Batch(params OfferDraftDeliveryAllocationDto[] allocations)
        => new()
        {
            Id = Guid.NewGuid(),
            BatchNumber = 1,
            DeliveryDate = new DateOnly(2026, 11, 1),
            Allocations = allocations.ToList()
        };

    private static OfferDraftDeliveryAllocationDto Allocation(
        Guid lineId,
        Guid packagingId,
        Material material,
        PackageType packageType,
        int packages)
        => new()
        {
            OfferLineId = lineId,
            OfferLinePackagingId = packagingId,
            MaterialId = material.Id,
            PackageTypeId = packageType.Id,
            Packages = packages
        };

    private static Setup CreateSetup()
    {
        var drum = new PackageType { Name = "Drum", NameAr = "برميل" };
        var materialA = new Material { ItemCode = "A", Name = "Material A", Category = "Polyol" };
        var materialB = new Material { ItemCode = "B", Name = "Material B", Category = "Iso" };
        var term = new PaymentTerm { Code = "transfer-order", Label = "Bank Transfer", LabelAr = "تحويل بنكي", Days = 0 };

        var catalog = new OfferCatalog
        {
            Customer = new Customer { Name = "Test Customer", Address = "Test address" },
            PaymentTerm = term,
            PaymentTerms = [term],
            Materials = [materialA, materialB],
            PackageTypes = [drum],
            CompanySettings = new CompanySettings { Name = "TECHNOCOM", ReferencePrefix = "YL" }
        };

        return new Setup(drum, materialA, materialB, catalog, term);
    }

    private sealed record Setup(
        PackageType Drum,
        Material MaterialA,
        Material MaterialB,
        OfferCatalog Catalog,
        PaymentTerm Term)
    {
        public OfferDraftDto Draft(
            params (Guid LineId, Material Material, (Guid PackagingId, int Packages)[] Packaging)[] lines)
            => new()
            {
                Reference = "Loc/100/YL/AA",
                OfferType = OfferType.Loc,
                IssueDate = new DateOnly(2026, 10, 21),
                CustomerId = Guid.NewGuid(),
                PaymentTermId = Term.Id,
                Lines = lines
                    .Select((line, index) => new OfferDraftLineDto
                    {
                        Id = line.LineId,
                        SortOrder = index + 1,
                        MaterialId = line.Material.Id,
                        RatePerKg = 10m,
                        Packaging = line.Packaging
                            .Select((packaging, packagingIndex) => new OfferDraftPackagingDto
                            {
                                Id = packaging.PackagingId,
                                SortOrder = packagingIndex + 1,
                                PackageTypeId = Drum.Id,
                                WeightPerPackageKg = 200m,
                                Packages = packaging.Packages
                            })
                            .ToList()
                    })
                    .ToList()
            };
    }
}
