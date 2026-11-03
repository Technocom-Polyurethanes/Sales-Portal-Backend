using sales_portal.core.Exceptions;
using sales_portal.core.Services;
using Xunit;

namespace SalesPortal.Tests;

public class ImportValidationTests
{
    [Fact]
    public async Task Missing_required_customer_fields_are_all_reported()
    {
        using var db = new TestDatabase();
        var preview = await ImportTestFiles.PreviewAsync(
            db.Context,
            "customers",
            ImportTestFiles.CustomerHeader + "\r\nCUST-EMPTY,,,,,,,,,,,,,,\r\n");

        var row = Assert.Single(preview.Rows);
        Assert.Equal("error", row.Status);
        Assert.Contains(row.Errors, error => error.Contains("name is required"));
        Assert.Contains(row.Errors, error => error.Contains("address is required"));
        Assert.Equal(1, preview.Summary.Error);
    }

    [Fact]
    public async Task Duplicate_codes_inside_the_file_are_reported_on_the_second_row()
    {
        using var db = new TestDatabase();
        var preview = await ImportTestFiles.PreviewAsync(
            db.Context,
            "materials",
            ImportTestFiles.MaterialHeader + "\r\n" +
            "MAT-1,,First,Polyol,10,,true,200\r\n" +
            "mat-1,,Second,Polyol,11,,true,200\r\n");

        Assert.Equal("new", preview.Rows[0].Status);
        Assert.Equal("error", preview.Rows[1].Status);
        Assert.Contains(preview.Rows[1].Errors, error => error.Contains("Duplicate item code"));
    }

    [Fact]
    public async Task Material_category_price_weight_and_active_flags_are_validated()
    {
        using var db = new TestDatabase();
        var preview = await ImportTestFiles.PreviewAsync(
            db.Context,
            "materials",
            ImportTestFiles.MaterialHeader + "\r\n" +
            "MAT-BAD,,Bad,Resin,-5,kg,maybe,-1\r\n");

        var row = Assert.Single(preview.Rows);
        Assert.Equal("error", row.Status);
        Assert.Contains(row.Errors, error => error.Contains("Category 'Resin' is not valid"));
        Assert.Contains(row.Errors, error => error.Contains("Reference price per kg cannot be negative"));
        Assert.Contains(row.Errors, error => error.Contains("Default weight cannot be negative"));
        Assert.Contains(row.Errors, error => error.Contains("Is active must be true or false"));
    }

    [Fact]
    public async Task Customer_currency_payment_term_and_email_are_validated()
    {
        using var db = new TestDatabase();
        var preview = await ImportTestFiles.PreviewAsync(
            db.Context,
            "customers",
            ImportTestFiles.CustomerHeader + "\r\n" +
            "CUST-BAD,,Bad Customer,,,,,not-an-email,,,,Address,,Net 99,XYZ\r\n");

        var row = Assert.Single(preview.Rows);
        Assert.Equal("error", row.Status);
        Assert.Contains(row.Errors, error => error.Contains("Email address 'not-an-email' is not valid"));
        Assert.Contains(row.Errors, error => error.Contains("Unknown currency 'XYZ'"));
        Assert.Contains(row.Errors, error => error.Contains("Unknown payment term 'Net 99'"));
    }

    [Fact]
    public async Task Payment_term_is_resolved_by_label_or_code()
    {
        using var db = new TestDatabase();
        var term = new sales_portal.domain.Models.PaymentTerm
        {
            Code = "cheque-30",
            Label = "Cheque Due 30 Days",
            LabelAr = "شيك بعد 30 يوم",
            Days = 30
        };
        db.Context.PaymentTerms.Add(term);
        await db.Context.SaveChangesAsync();

        var preview = await ImportTestFiles.PreviewAsync(
            db.Context,
            "customers",
            ImportTestFiles.CustomerHeader + "\r\n" +
            "CUST-TERM,,Term Customer,,,,,,,,,Address,,cheque-30,\r\n" +
            "CUST-TERM-AR,,عميل,,,,,,,,,عنوان,,شيك بعد 30 يوم,\r\n");

        Assert.Equal("new", preview.Rows[0].Status);
        Assert.Equal("Cheque Due 30 Days", preview.Rows[0].Values["defaultPaymentTerm"]);
        Assert.Equal("new", preview.Rows[1].Status);
        Assert.Equal("Cheque Due 30 Days", preview.Rows[1].Values["defaultPaymentTerm"]);
    }

    [Fact]
    public async Task Price_rows_need_an_existing_material_and_a_price()
    {
        using var db = new TestDatabase();
        db.Context.Materials.Add(new sales_portal.domain.Models.Material
        {
            ItemCode = "MAT-PRICE",
            Name = "Priced",
            Category = "Polyol",
            ReferencePricePerKg = 10m
        });
        await db.Context.SaveChangesAsync();

        var preview = await ImportTestFiles.PreviewAsync(
            db.Context,
            "prices",
            ImportTestFiles.PriceHeader + "\r\n" +
            "MAT-PRICE,12.5\r\n" +
            "MAT-MISSING,10\r\n" +
            "MAT-PRICE,\r\n");

        Assert.Equal("update", preview.Rows[0].Status);
        Assert.Single(preview.Rows[0].Changes);
        Assert.Equal("10", preview.Rows[0].Changes[0].OldValue);
        Assert.Equal("12.5", preview.Rows[0].Changes[0].NewValue);
        Assert.Equal("error", preview.Rows[1].Status);
        Assert.Contains(preview.Rows[1].Errors, error => error.Contains("No material matches item code"));
        Assert.Equal("error", preview.Rows[2].Status);
        Assert.Contains(preview.Rows[2].Errors, error => error.Contains("Duplicate item code"));
    }

    [Fact]
    public async Task File_without_data_rows_is_rejected()
    {
        using var db = new TestDatabase();
        var stream = ImportTestFiles.Csv(ImportTestFiles.PriceHeader + "\r\n");
        var service = TestFactory.CreateImportService(db.Context);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.PreviewAsync("prices", stream, "prices.csv", stream.Length, null, CancellationToken.None));

        Assert.Contains("no data rows", exception.Message);
    }
}
