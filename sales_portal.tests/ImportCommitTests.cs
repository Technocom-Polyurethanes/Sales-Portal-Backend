using Microsoft.EntityFrameworkCore;
using sales_portal.core.Services;
using sales_portal.domain.Models;
using Xunit;

namespace SalesPortal.Tests;

public class ImportCommitTests
{
    [Fact]
    public async Task Commit_updates_an_existing_customer_and_is_idempotent()
    {
        using var db = new TestDatabase();
        db.Context.Customers.Add(new Customer
        {
            CustomerCode = "CUST-1",
            Name = "Before",
            Address = "Old address"
        });
        await db.Context.SaveChangesAsync();

        var service = TestFactory.CreateImportService(db.Context);
        var preview = await PreviewAsync(
            service,
            "customers",
            ImportTestFiles.CustomerHeader + "\r\n" +
            "CUST-1,,After,,,,,,,,,New address,,\r\n");

        var row = Assert.Single(preview.Rows);
        Assert.Equal("update", row.Status);
        Assert.Equal(2, row.Changes.Count);
        Assert.Contains(row.Changes, change => change.Field == "name");
        Assert.Contains(row.Changes, change => change.Field == "address");

        var first = await service.CommitAsync(preview.ImportId, CancellationToken.None);
        Assert.Equal(0, first.Created);
        Assert.Equal(1, first.Updated);

        var stored = await db.Context.Customers.SingleAsync(customer => customer.CustomerCode == "CUST-1");
        Assert.Equal("After", stored.Name);
        Assert.Equal("New address", stored.Address);

        var second = await service.CommitAsync(preview.ImportId, CancellationToken.None);
        Assert.Equal(first.Updated, second.Updated);
        Assert.Equal(first.Created, second.Created);
        Assert.Equal(1, await db.Context.Customers.CountAsync());
    }

    [Fact]
    public async Task Commit_creates_new_rows_and_skips_error_rows()
    {
        using var db = new TestDatabase();
        var service = TestFactory.CreateImportService(db.Context);
        var preview = await PreviewAsync(
            service,
            "materials",
            ImportTestFiles.MaterialHeader + "\r\n" +
            "MAT-NEW,,New material,Polyol,10,,true,200\r\n" +
            "MAT-ERR,,Bad material,Resin,10,,true,200\r\n");

        Assert.Equal(2, preview.Summary.Total);

        var result = await service.CommitAsync(preview.ImportId, CancellationToken.None);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Failed);
        var created = await db.Context.Materials.SingleAsync(material => material.ItemCode == "MAT-NEW");
        Assert.Equal("Polyol", created.Category);
        Assert.Equal(10m, created.ReferencePricePerKg);
        Assert.False(await db.Context.Materials.AnyAsync(material => material.ItemCode == "MAT-ERR"));
    }

    [Fact]
    public async Task Unchanged_rows_are_skipped_and_price_updates_apply()
    {
        using var db = new TestDatabase();
        db.Context.Materials.Add(new Material
        {
            ItemCode = "MAT-P",
            Name = "Priced",
            Category = "Polyol",
            ReferencePricePerKg = 10m
        });
        await db.Context.SaveChangesAsync();

        var service = TestFactory.CreateImportService(db.Context);
        var preview = await PreviewAsync(
            service,
            "prices",
            ImportTestFiles.PriceHeader + "\r\n" +
            "MAT-P,10\r\n" +
            "MAT-P,15\r\n");

        // The second row repeats the code, so only the first row is valid and unchanged.
        Assert.Equal("unchanged", preview.Rows[0].Status);
        Assert.Equal("error", preview.Rows[1].Status);

        var result = await service.CommitAsync(preview.ImportId, CancellationToken.None);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(1, result.Failed);
        Assert.Equal(10m, (await db.Context.Materials.SingleAsync()).ReferencePricePerKg);
    }

    [Fact]
    public async Task Price_preview_diff_and_update_are_applied()
    {
        using var db = new TestDatabase();
        db.Context.Materials.Add(new Material
        {
            ItemCode = "MAT-P2",
            Name = "Priced",
            Category = "Polyol",
            ReferencePricePerKg = 10m
        });
        await db.Context.SaveChangesAsync();

        var service = TestFactory.CreateImportService(db.Context);
        var preview = await PreviewAsync(
            service,
            "prices",
            ImportTestFiles.PriceHeader + "\r\nMAT-P2,15\r\n");

        var result = await service.CommitAsync(preview.ImportId, CancellationToken.None);

        Assert.Equal(1, result.Updated);
        Assert.Equal(15m, (await db.Context.Materials.SingleAsync()).ReferencePricePerKg);
    }

    private static Task<sales_portal.core.DTOs.ImportPreviewDto> PreviewAsync(
        ImportService service,
        string type,
        string csv)
    {
        var stream = ImportTestFiles.Csv(csv);
        return service.PreviewAsync(
            type, stream, $"{type}.csv", stream.Length, null, CancellationToken.None);
    }
}
