using Microsoft.Extensions.Caching.Memory;
using sales_portal.core.Caching;
using sales_portal.core.DTOs;
using sales_portal.core.Services;
using sales_portal.domain.Models;
using sales_portal.infrastructure;
using Xunit;

namespace SalesPortal.Tests;

public class ReferenceDataTests
{
    private static ReferenceDataCache NewCache() => new(new MemoryCache(new MemoryCacheOptions()));

    private static LookupQueryDto FirstPage() => new() { Page = 1, PageSize = 50 };

    private static Customer NewCustomer(string code, string name, string? nameAr = null) => new()
    {
        CustomerCode = code,
        Name = name,
        NameAr = nameAr,
        Address = "Test address"
    };

    private static Material NewMaterial(string code, string name, bool isActive = true) => new()
    {
        ItemCode = code,
        Name = name,
        Category = "Polyol",
        IsActive = isActive
    };

    [Fact]
    public void Natural_comparer_orders_codes_the_way_they_read()
    {
        var codes = new[] { "CUST-10", "CUST-2", "cust-1", "50001", "9", "30001", "10", "2" };

        var sorted = codes.OrderBy(code => code, NaturalStringComparer.Instance).ToArray();

        Assert.Equal(
            new[] { "2", "9", "10", "30001", "50001", "cust-1", "CUST-2", "CUST-10" },
            sorted);
    }

    [Fact]
    public async Task Customer_lists_are_ordered_by_customer_code_not_by_name()
    {
        using var db = new TestDatabase();
        db.Context.Customers.AddRange(
            NewCustomer("CUST-10", "Alpha"),
            NewCustomer("CUST-2", "Zulu"),
            NewCustomer("CUST-1", "Mike"));
        await db.Context.SaveChangesAsync();
        var service = TestFactory.CreateCustomerService(db.Context);
        var expected = new[] { "CUST-1", "CUST-2", "CUST-10" };

        var all = await service.GetAllAsync(null, CancellationToken.None);
        var paged = await service.GetPagedAsync(null, 1, 50, CancellationToken.None);
        var lookup = await service.LookupAsync(FirstPage(), CancellationToken.None);

        Assert.Equal(expected, all.Select(customer => customer.CustomerCode));
        Assert.Equal(expected, paged.Items.Select(customer => customer.CustomerCode));
        Assert.Equal(expected, lookup.Items.Select(customer => customer.CustomerCode));
    }

    [Fact]
    public async Task Customer_search_matches_code_name_and_arabic_name_and_keeps_the_order()
    {
        using var db = new TestDatabase();
        db.Context.Customers.AddRange(
            NewCustomer("CUST-10", "Alpha Trading"),
            NewCustomer("CUST-2", "Zulu Foam", "شركة الزولو"),
            NewCustomer("CUST-1", "Mike Foam"));
        await db.Context.SaveChangesAsync();
        var service = TestFactory.CreateCustomerService(db.Context);

        var byName = await service.LookupAsync(
            new LookupQueryDto { Search = "foam", Page = 1, PageSize = 50 },
            CancellationToken.None);
        var byCode = await service.LookupAsync(
            new LookupQueryDto { Search = "cust-10", Page = 1, PageSize = 50 },
            CancellationToken.None);
        var byArabic = await service.LookupAsync(
            new LookupQueryDto { Search = "الزولو", Page = 1, PageSize = 50 },
            CancellationToken.None);

        Assert.Equal(new[] { "CUST-1", "CUST-2" }, byName.Items.Select(customer => customer.CustomerCode));
        Assert.Equal(2, byName.TotalCount);
        Assert.Equal("CUST-10", Assert.Single(byCode.Items).CustomerCode);
        Assert.Equal("CUST-2", Assert.Single(byArabic.Items).CustomerCode);
    }

    [Fact]
    public async Task Material_lists_are_ordered_by_item_code_and_lookup_skips_inactive_materials()
    {
        using var db = new TestDatabase();
        db.Context.Materials.AddRange(
            NewMaterial("50001", "Daltorim"),
            NewMaterial("9", "Short code"),
            NewMaterial("30001", "Suprasec"),
            NewMaterial("20000", "Retired", isActive: false));
        await db.Context.SaveChangesAsync();
        var service = new MaterialService(new Repository<Material>(db.Context));

        var all = await service.GetAllAsync(null, null, null, CancellationToken.None);
        var lookup = await service.LookupAsync(FirstPage(), CancellationToken.None);

        Assert.Equal(
            new[] { "9", "20000", "30001", "50001" },
            all.Select(material => material.ItemCode));
        Assert.Equal(
            new[] { "9", "30001", "50001" },
            lookup.Items.Select(material => material.ItemCode));
    }

    [Fact]
    public async Task Customer_lookup_is_served_from_cache_until_a_customer_is_written()
    {
        using var db = new TestDatabase();
        db.Context.Customers.Add(NewCustomer("CUST-1", "One"));
        await db.Context.SaveChangesAsync();
        var service = new CustomerService(new Repository<Customer>(db.Context), NewCache());

        var first = await service.LookupAsync(FirstPage(), CancellationToken.None);
        Assert.Single(first.Items);

        // Written behind the service's back, so nothing told the cache: the old list is served.
        db.Context.Customers.Add(NewCustomer("CUST-2", "Two"));
        await db.Context.SaveChangesAsync();
        var cached = await service.LookupAsync(FirstPage(), CancellationToken.None);
        Assert.Single(cached.Items);

        // A write through the service drops the cache, so the next read sees every customer.
        await service.CreateAsync(
            new UpsertCustomerDto { CustomerCode = "CUST-3", Name = "Three", Address = "Test address" },
            CancellationToken.None);
        var fresh = await service.LookupAsync(FirstPage(), CancellationToken.None);
        Assert.Equal(
            new[] { "CUST-1", "CUST-2", "CUST-3" },
            fresh.Items.Select(customer => customer.CustomerCode));
    }

    [Fact]
    public async Task Material_lookup_is_served_from_cache_until_a_material_is_written()
    {
        using var db = new TestDatabase();
        db.Context.Materials.Add(NewMaterial("MAT-1", "One"));
        await db.Context.SaveChangesAsync();
        var service = new MaterialService(new Repository<Material>(db.Context), NewCache());

        Assert.Single((await service.LookupAsync(FirstPage(), CancellationToken.None)).Items);

        db.Context.Materials.Add(NewMaterial("MAT-2", "Two"));
        await db.Context.SaveChangesAsync();
        Assert.Single((await service.LookupAsync(FirstPage(), CancellationToken.None)).Items);

        await service.CreateAsync(
            new UpsertMaterialDto { ItemCode = "MAT-3", Name = "Three", Category = "Polyol" },
            CancellationToken.None);
        var fresh = await service.LookupAsync(FirstPage(), CancellationToken.None);
        Assert.Equal(3, fresh.Items.Count);
    }

    [Fact]
    public async Task Offers_are_ordered_by_issue_month_then_serial_number_ascending_by_default()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        // Ordering groups by issue month before serial, so November 5 comes after October 12.
        TestFactory.AddOffer(db.Context, basics, "Loc/5/YL/AA", issueDate: new DateOnly(2026, 11, 3), serialNumber: 5);
        TestFactory.AddOffer(db.Context, basics, "Loc/12/YL/AA", issueDate: new DateOnly(2026, 10, 4), serialNumber: 12);
        TestFactory.AddOffer(db.Context, basics, "Loc/3/YL/AA", issueDate: new DateOnly(2026, 10, 20), serialNumber: 3);
        var service = TestFactory.CreateOfferService(db.Context);

        var ascending = await service.GetListAsync(new OfferQueryParamsDto(), CancellationToken.None);
        var descending = await service.GetListAsync(
            new OfferQueryParamsDto { SortDir = "desc" },
            CancellationToken.None);

        Assert.Equal(
            new[] { "Loc/3/YL/AA", "Loc/12/YL/AA", "Loc/5/YL/AA" },
            ascending.Items.Select(offer => offer.Reference));
        Assert.Equal(
            new[] { "Loc/5/YL/AA", "Loc/12/YL/AA", "Loc/3/YL/AA" },
            descending.Items.Select(offer => offer.Reference));
    }
}
