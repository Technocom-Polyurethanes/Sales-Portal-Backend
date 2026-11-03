using ClosedXML.Excel;
using sales_portal.core.Services;
using Xunit;

namespace SalesPortal.Tests;

public class ImportParsingTests
{
    [Fact]
    public async Task Csv_with_BOM_quotes_and_Arabic_text_is_parsed()
    {
        using var db = new TestDatabase();
        var stream = ImportTestFiles.BomCsv(
            ImportTestFiles.CustomerHeader + "\r\n" +
            "CUST-AR,ERP-1,\"شركة, أو\",شركة عربية,,,,,,,القاهرة,\"Cairo, Egypt\",عنوان عربي,,EGP\r\n");

        var service = TestFactory.CreateImportService(db.Context);
        var preview = await service.PreviewAsync(
            "customers", stream, "customers.csv", stream.Length, null, CancellationToken.None);

        var row = Assert.Single(preview.Rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("شركة, أو", row.Values["name"]);
        Assert.Equal("شركة عربية", row.Values["nameAr"]);
        Assert.Equal("Cairo, Egypt", row.Values["address"]);
        Assert.Equal("عنوان عربي", row.Values["addressAr"]);
        Assert.Equal("القاهرة", row.Values["cityAr"]);
        Assert.Equal("new", row.Status);
    }

    [Fact]
    public async Task Excel_workbook_is_parsed_with_the_template_headers()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");
        var headers = ImportTestFiles.MaterialHeader.Split(',');
        for (var index = 0; index < headers.Length; index += 1)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }

        sheet.Cell(2, 1).Value = "MAT-XLSX";
        sheet.Cell(2, 3).Value = "Excel Material";
        sheet.Cell(2, 4).Value = "Polyol";
        sheet.Cell(2, 5).Value = 12.5m;
        sheet.Cell(2, 8).Value = 220;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        using var db = new TestDatabase();
        var service = TestFactory.CreateImportService(db.Context);
        var preview = await service.PreviewAsync(
            "materials", stream, "materials.xlsx", stream.Length, null, CancellationToken.None);

        var row = Assert.Single(preview.Rows);
        Assert.Equal("MAT-XLSX", row.Values["itemCode"]);
        Assert.Equal("Polyol", row.Values["category"]);
        Assert.Equal("12.5", row.Values["referencePricePerKg"]);
        Assert.Equal("220", row.Values["defaultWeightKg"]);
        Assert.Equal("new", row.Status);
    }

    [Fact]
    public async Task Template_download_has_the_contract_columns()
    {
        using var db = new TestDatabase();
        var service = TestFactory.CreateImportService(db.Context);

        var template = await service.GetTemplateAsync("prices", CancellationToken.None);
        var text = System.Text.Encoding.UTF8.GetString(template.Content);
        var rows = text.Split("\r\n");

        Assert.Equal("prices-template.csv", template.FileName);
        Assert.Equal(ImportTestFiles.PriceHeader, rows[0].TrimStart('\uFEFF'));
        Assert.Equal(0xEF, template.Content[0]);
    }

    [Fact]
    public async Task Unsupported_extension_is_rejected()
    {
        using var db = new TestDatabase();
        var service = TestFactory.CreateImportService(db.Context);
        var stream = ImportTestFiles.Csv("a,b\r\n1,2\r\n");

        var exception = await Assert.ThrowsAsync<sales_portal.core.Exceptions.ValidationException>(() =>
            service.PreviewAsync("customers", stream, "customers.xls", stream.Length, null, CancellationToken.None));

        Assert.Contains(".xlsx or .csv", exception.Message);
    }
}
