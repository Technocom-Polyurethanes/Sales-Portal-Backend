using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using sales_portal.core.Exceptions;
using ValidationException = sales_portal.core.Exceptions.ValidationException;

namespace sales_portal.core.Services.Import;

internal sealed record ParsedImportRow(int RowNumber, Dictionary<string, string?> Values);

/// <summary>Reads the uploaded .xlsx or .csv file into canonical template columns.</summary>
internal static class ImportFileReader
{
    public static List<ParsedImportRow> Read(Stream stream, string fileName, ImportKind kind)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var parsed = extension switch
        {
            ".csv" => ReadCsv(stream, kind),
            ".xlsx" => ReadExcel(stream, kind),
            _ => throw new ValidationException("Upload an .xlsx or .csv file.")
        };

        return parsed
            .Where(row => row.Values.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToList();
    }

    private static List<ParsedImportRow> ReadCsv(Stream stream, ImportKind kind)
    {
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null,
            DetectColumnCountChanges = false
        };

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        using var csv = new CsvReader(reader, configuration);

        if (!csv.Read() || !csv.ReadHeader())
        {
            return [];
        }

        var indexes = MapIndexes(csv.HeaderRecord ?? [], kind);
        var rows = new List<ParsedImportRow>();

        while (csv.Read())
        {
            var record = csv.Parser.Record ?? [];
            rows.Add(new ParsedImportRow(csv.Parser.Row, ToValues(record, indexes, kind)));
        }

        return rows;
    }

    private static List<ParsedImportRow> ReadExcel(Stream stream, ImportKind kind)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new ValidationException("The workbook has no worksheet.");

        var usedRows = sheet.RowsUsed().ToList();
        if (usedRows.Count == 0)
        {
            return [];
        }

        var headerCells = usedRows[0].Cells(1, kind.Columns().Length + 1).ToList();
        var indexes = MapIndexes(headerCells.Select(CellText).ToArray(), kind);

        var rows = new List<ParsedImportRow>();
        foreach (var row in usedRows.Skip(1))
        {
            var cells = row.Cells(1, kind.Columns().Length + 1).Select(CellText).ToArray();
            rows.Add(new ParsedImportRow(row.RowNumber(), ToValues(cells, indexes, kind)));
        }

        return rows;
    }

    private static string CellText(IXLCell cell)
        => cell.Value.ToString(CultureInfo.InvariantCulture).Trim();

    private static Dictionary<string, int> MapIndexes(string[] headers, ImportKind kind)
    {
        var columns = kind.Columns();
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < headers.Length; index += 1)
        {
            var header = headers[index].Trim();
            if (header.Length == 0) continue;

            var column = columns.FirstOrDefault(
                name => string.Equals(name, header, StringComparison.OrdinalIgnoreCase));
            if (column != null)
            {
                lookup[column] = index;
            }
        }

        return lookup;
    }

    private static Dictionary<string, string?> ToValues(
        string[] record,
        Dictionary<string, int> indexes,
        ImportKind kind)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var column in kind.Columns())
        {
            values[column] = indexes.TryGetValue(column, out var index) && index < record.Length
                ? NullIfEmpty(record[index])
                : null;
        }

        return values;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
