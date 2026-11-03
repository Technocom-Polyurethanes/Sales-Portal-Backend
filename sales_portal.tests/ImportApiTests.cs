using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using sales_portal.core.DTOs;
using Xunit;

namespace SalesPortal.Tests;

[Collection(ProcessEnvironment.Name)]
public class ImportApiTests : IClassFixture<SalesPortalApiFactory>
{
    private readonly SalesPortalApiFactory _factory;

    public ImportApiTests(SalesPortalApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Template_endpoint_returns_the_contract_headers()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/import/templates/materials");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("materials-template.csv", response.Content.Headers.ContentDisposition?.FileName);

        var body = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("itemCode,erpNextItemCode,name,category", body);
    }

    [Fact]
    public async Task Preview_commit_and_history_round_trip()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var code = $"CUST-API-{Guid.NewGuid():N}";
        var csv = ImportTestFiles.CustomerHeader + "\r\n" +
                  $"{code},,API Customer,,,,,,,,,API address,,\r\n";

        var previewResponse = await PostFileAsync(client, "customers", csv);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);

        var preview = await previewResponse.Content.ReadFromJsonAsync<ImportPreviewDto>();
        Assert.NotNull(preview);
        Assert.NotEqual(Guid.Empty, preview!.ImportId);
        Assert.Equal(1, preview.Summary.New);
        var row = Assert.Single(preview.Rows);
        Assert.Equal("new", row.Status);
        Assert.Equal(code, row.Values["customerCode"]);

        var commitResponse = await client.PostAsync(
            $"/api/import/{preview.ImportId}/commit", content: null);
        Assert.Equal(HttpStatusCode.OK, commitResponse.StatusCode);
        var result = await commitResponse.Content.ReadFromJsonAsync<ImportCommitResultDto>();
        Assert.NotNull(result);
        Assert.Equal(1, result!.Created);
        Assert.Equal(0, result.Failed);

        var historyResponse = await client.GetAsync("/api/import/history?page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<PaginatedResultDto<ImportHistoryItemDto>>();
        Assert.NotNull(history);
        Assert.Contains(history!.Items, item => item.Id == preview.ImportId);
    }

    [Fact]
    public async Task Preview_without_a_file_returns_problem_details()
    {
        using var client = _factory.CreateAuthenticatedClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([]), "file", "empty.csv");

        var response = await client.PostAsync("/api/import/customers/preview", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Choose a file", body);
    }

    [Fact]
    public async Task Preview_requires_the_admin_role()
    {
        using var client = _factory.CreateAuthenticatedClient(role: "Sales");
        var csv = ImportTestFiles.CustomerHeader + "\r\n";

        var response = await PostFileAsync(client, "customers", csv);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_import_type_returns_problem_details()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/import/templates/offers");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unknown import type", body);
    }

    private static async Task<HttpResponseMessage> PostFileAsync(
        HttpClient client,
        string type,
        string csv)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", $"{type}.csv");
        return await client.PostAsync($"/api/import/{type}/preview", form);
    }
}
