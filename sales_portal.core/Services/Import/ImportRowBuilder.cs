using System.Globalization;
using System.Net.Mail;
using sales_portal.core.DTOs;
using sales_portal.domain.Models;

namespace sales_portal.core.Services.Import;

internal sealed class ImportLookups
{
    public List<Customer> Customers { get; init; } = new();
    public List<Material> Materials { get; init; } = new();
    public List<PaymentTerm> PaymentTerms { get; init; } = new();
    public List<BankAccount> BankAccounts { get; init; } = new();
}

/// <summary>
/// Turns parsed rows into preview rows: validates every cell, matches existing records
/// and computes the diff. All errors of a row are collected, never just the first one.
/// </summary>
internal static class ImportRowBuilder
{
    private static readonly HashSet<string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Polyol", "Iso", "System", "Additive"
    };

    private static readonly HashSet<string> Currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "EGP", "USD", "EUR", "GBP", "SAR", "AED", "KWD", "QAR"
    };

    public static List<StoredImportRow> Build(
        ImportKind kind,
        List<ParsedImportRow> parsed,
        ImportLookups lookups)
    {
        var rows = new List<StoredImportRow>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in parsed)
        {
            var row = new StoredImportRow
            {
                RowNumber = item.RowNumber,
                Values = item.Values
            };

            switch (kind)
            {
                case ImportKind.Customers:
                    BuildCustomer(row, lookups, seenCodes);
                    break;
                case ImportKind.Materials:
                    BuildMaterial(row, lookups, seenCodes);
                    break;
                case ImportKind.BankAccounts:
                    BuildBankAccount(row, lookups, seenCodes, rows.Count + 1);
                    break;
                default:
                    BuildPrice(row, lookups, seenCodes);
                    break;
            }

            row.Status = row.Errors.Count > 0
                ? "error"
                : row.Existing == null
                    ? "new"
                    : row.Changes.Count == 0 ? "unchanged" : "update";

            rows.Add(row);
        }

        return rows;
    }

    private static void BuildCustomer(
        StoredImportRow row,
        ImportLookups lookups,
        HashSet<string> seenCodes)
    {
        var values = row.Values;
        var code = values["customerCode"];
        var erpId = values["erpNextId"];

        if (code == null)
        {
            row.Errors.Add("Customer code is required.");
        }
        else if (!seenCodes.Add(code))
        {
            row.Errors.Add($"Duplicate customer code '{code}' in this file.");
        }

        var customer = FindCustomer(lookups, code, erpId);
        row.TargetId = customer?.Id;

        if (customer == null)
        {
            if (values["name"] == null) row.Errors.Add("Customer name is required.");
            if (values["address"] == null) row.Errors.Add("Customer address is required.");
        }

        if (values["email"] is { } email && !MailAddress.TryCreate(email, out _))
        {
            row.Errors.Add($"Email address '{email}' is not valid.");
        }

        var currency = values["defaultCurrency"]?.ToUpperInvariant();
        if (currency != null)
        {
            if (!Currencies.Contains(currency))
            {
                row.Errors.Add($"Unknown currency '{currency}'.");
            }
            else
            {
                values["defaultCurrency"] = currency;
            }
        }
        else if (customer == null)
        {
            values["defaultCurrency"] = "EGP";
        }

        if (values["defaultPaymentTerm"] is { } termText)
        {
            var term = lookups.PaymentTerms.FirstOrDefault(candidate =>
                string.Equals(candidate.Code, termText, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Label, termText, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.LabelAr, termText, StringComparison.OrdinalIgnoreCase));

            if (term == null)
            {
                row.Errors.Add($"Unknown payment term '{termText}'.");
            }
            else
            {
                row.PaymentTermId = term.Id;
                values["defaultPaymentTerm"] = term.Label;
            }
        }

        if (customer != null)
        {
            row.Existing = CustomerSnapshot(customer, lookups);
            AddChanges(row);
        }
    }

    private static void BuildMaterial(
        StoredImportRow row,
        ImportLookups lookups,
        HashSet<string> seenCodes)
    {
        var values = row.Values;
        var code = values["itemCode"];

        if (code == null)
        {
            row.Errors.Add("Item code is required.");
        }
        else if (!seenCodes.Add(code))
        {
            row.Errors.Add($"Duplicate item code '{code}' in this file.");
        }

        var material = FindMaterial(lookups, code, values["erpNextItemCode"]);
        row.TargetId = material?.Id;

        if (material == null)
        {
            if (values["name"] == null) row.Errors.Add("Material name is required.");
            if (values["category"] == null) row.Errors.Add("Material category is required.");
        }

        if (values["category"] is { } category)
        {
            var canonical = Categories.FirstOrDefault(
                allowed => string.Equals(allowed, category, StringComparison.OrdinalIgnoreCase));
            if (canonical == null)
            {
                row.Errors.Add(
                    $"Category '{category}' is not valid. Use Polyol, Iso, System or Additive.");
            }
            else
            {
                values["category"] = canonical;
            }
        }

        var price = ParseDecimal(values["referencePricePerKg"], "Reference price per kg", row.Errors);
        values["referencePricePerKg"] = price;

        var weight = ParseDecimal(values["defaultWeightKg"], "Default weight", row.Errors);
        values["defaultWeightKg"] = weight;

        if (values["isActive"] is { } active)
        {
            var parsed = ParseBool(active);
            if (parsed == null)
            {
                row.Errors.Add("Is active must be true or false.");
            }
            else
            {
                values["isActive"] = parsed.Value ? "true" : "false";
            }
        }
        else if (material == null)
        {
            values["isActive"] = "true";
        }

        if (values["uom"] == null && material == null)
        {
            values["uom"] = "kg";
        }

        if (material != null)
        {
            row.Existing = MaterialSnapshot(material);
            AddChanges(row);
        }
    }

    private static void BuildBankAccount(
        StoredImportRow row,
        ImportLookups lookups,
        HashSet<string> seenCodes,
        int position)
    {
        var values = row.Values;
        var accountNumber = values["accountNumber"]?.Trim();

        if (values["bank"] == null) row.Errors.Add("Bank is required.");
        if (values["branch"] == null) row.Errors.Add("Branch is required.");

        if (accountNumber == null)
        {
            row.Errors.Add("Account number is required.");
        }
        else
        {
            values["accountNumber"] = accountNumber;
            if (!seenCodes.Add(accountNumber))
            {
                row.Errors.Add($"Duplicate account number '{accountNumber}' in this file.");
            }
        }

        var match = accountNumber == null
            ? null
            : FindBankAccount(lookups, accountNumber);
        row.TargetId = match?.Id;

        if (accountNumber != null && match == null &&
            lookups.BankAccounts.Count(account =>
                string.Equals(account.AccountNumber.Trim(), accountNumber, StringComparison.OrdinalIgnoreCase)) > 1)
        {
            row.Errors.Add(
                $"Account number '{accountNumber}' is ambiguous: more than one bank account already uses it.");
        }

        if (values["currency"] is { } currency)
        {
            values["currency"] = currency.Trim().ToUpperInvariant();
        }
        else if (match == null)
        {
            values["currency"] = "EGP";
        }

        if (values["isActive"] is { } active)
        {
            var parsed = ParseBool(active);
            if (parsed == null)
            {
                row.Errors.Add("Is active must be true or false.");
            }
            else
            {
                values["isActive"] = parsed.Value ? "true" : "false";
            }
        }
        else if (match == null)
        {
            values["isActive"] = "true";
        }

        if (values["sortOrder"] is { } sortOrder)
        {
            if (!int.TryParse(sortOrder, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                row.Errors.Add("Sort order must be a whole number.");
            }
        }
        else if (match == null)
        {
            var maxSortOrder = lookups.BankAccounts.Count == 0
                ? 0
                : lookups.BankAccounts.Max(account => account.SortOrder);
            values["sortOrder"] = (maxSortOrder + position).ToString(CultureInfo.InvariantCulture);
        }

        if (match != null)
        {
            // Keep the stored spelling so a case-only difference is not reported as a change.
            values["accountNumber"] = match.AccountNumber;
            row.Existing = BankAccountSnapshot(match);
            AddChanges(row);
        }
    }

    private static BankAccount? FindBankAccount(ImportLookups lookups, string accountNumber)
    {
        var matches = lookups.BankAccounts
            .Where(account => string.Equals(
                account.AccountNumber.Trim(),
                accountNumber,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static void BuildPrice(
        StoredImportRow row,
        ImportLookups lookups,
        HashSet<string> seenCodes)
    {
        var values = row.Values;
        var code = values["itemCode"];

        if (code == null)
        {
            row.Errors.Add("Item code is required.");
        }
        else if (!seenCodes.Add(code))
        {
            row.Errors.Add($"Duplicate item code '{code}' in this file.");
        }

        var material = FindMaterial(lookups, code, null);
        row.TargetId = material?.Id;
        if (material == null && code != null)
        {
            row.Errors.Add($"No material matches item code '{code}'.");
        }

        if (values["referencePricePerKg"] == null)
        {
            row.Errors.Add("Reference price per kg is required.");
        }

        var price = ParseDecimal(values["referencePricePerKg"], "Reference price per kg", row.Errors);
        values["referencePricePerKg"] = price;

        if (material != null)
        {
            row.Existing = new Dictionary<string, string?>
            {
                ["itemCode"] = material.ItemCode,
                ["referencePricePerKg"] = FormatDecimal(material.ReferencePricePerKg)
            };
            AddChanges(row);
        }
    }

    private static Customer? FindCustomer(ImportLookups lookups, string? code, string? erpId)
    {
        if (code != null)
        {
            var byCode = lookups.Customers.FirstOrDefault(
                customer => string.Equals(customer.CustomerCode, code, StringComparison.OrdinalIgnoreCase));
            if (byCode != null) return byCode;
        }

        if (erpId == null) return null;

        return lookups.Customers.FirstOrDefault(
            customer => string.Equals(customer.ErpNextId, erpId, StringComparison.OrdinalIgnoreCase));
    }

    private static Material? FindMaterial(ImportLookups lookups, string? code, string? erpCode)
    {
        if (code != null)
        {
            var byCode = lookups.Materials.FirstOrDefault(
                material => string.Equals(material.ItemCode, code, StringComparison.OrdinalIgnoreCase));
            if (byCode != null) return byCode;
        }

        if (erpCode == null) return null;

        return lookups.Materials.FirstOrDefault(
            material => string.Equals(material.ErpNextItemCode, erpCode, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string?> CustomerSnapshot(
        Customer customer,
        ImportLookups lookups)
    {
        var termLabel = customer.DefaultPaymentTermId == null
            ? null
            : lookups.PaymentTerms
                .FirstOrDefault(term => term.Id == customer.DefaultPaymentTermId)?.Label;

        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["customerCode"] = customer.CustomerCode,
            ["erpNextId"] = customer.ErpNextId,
            ["name"] = customer.Name,
            ["nameAr"] = customer.NameAr,
            ["taxId"] = customer.TaxId,
            ["contactPerson"] = customer.ContactPerson,
            ["contactPersonAr"] = customer.ContactPersonAr,
            ["email"] = customer.Email,
            ["phone"] = customer.Phone,
            ["city"] = customer.City,
            ["cityAr"] = customer.CityAr,
            ["address"] = customer.Address,
            ["addressAr"] = customer.AddressAr,
            ["defaultPaymentTerm"] = termLabel,
            ["defaultCurrency"] = customer.DefaultCurrency
        };
    }

    private static Dictionary<string, string?> MaterialSnapshot(Material material) =>
        new(StringComparer.Ordinal)
        {
            ["itemCode"] = material.ItemCode,
            ["erpNextItemCode"] = material.ErpNextItemCode,
            ["name"] = material.Name,
            ["category"] = material.Category,
            ["referencePricePerKg"] = FormatDecimal(material.ReferencePricePerKg),
            ["uom"] = material.Uom,
            ["isActive"] = material.IsActive ? "true" : "false",
            ["defaultWeightKg"] = FormatDecimal(material.DefaultWeightKg)
        };

    private static Dictionary<string, string?> BankAccountSnapshot(BankAccount account) =>
        new(StringComparer.Ordinal)
        {
            ["bank"] = account.Bank,
            ["bankAr"] = account.BankAr,
            ["branch"] = account.Branch,
            ["branchAr"] = account.BranchAr,
            ["accountHolderName"] = account.AccountHolderName,
            ["accountHolderNameAr"] = account.AccountHolderNameAr,
            ["accountNumber"] = account.AccountNumber,
            ["swift"] = account.Swift,
            ["iban"] = account.Iban,
            ["currency"] = account.Currency,
            ["isActive"] = account.IsActive ? "true" : "false",
            ["sortOrder"] = account.SortOrder.ToString(CultureInfo.InvariantCulture)
        };

    /// <summary>
    /// Empty cells leave the stored value untouched, so the diff only covers the
    /// columns the file actually filled in.
    /// </summary>
    private static void AddChanges(StoredImportRow row)
    {
        foreach (var (field, value) in row.Values)
        {
            if (value == null) continue;

            row.Existing!.TryGetValue(field, out var oldValue);
            if (!string.Equals(value, oldValue, StringComparison.Ordinal))
            {
                row.Changes.Add(new ImportChangeDto
                {
                    Field = field,
                    OldValue = oldValue,
                    NewValue = value
                });
            }
        }
    }

    private static string? ParseDecimal(string? text, string label, List<string> errors)
    {
        if (text == null) return null;

        if (!decimal.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value))
        {
            errors.Add($"{label} must be a number.");
            return text;
        }

        if (value < 0m)
        {
            errors.Add($"{label} cannot be negative.");
            return text;
        }

        return FormatDecimal(value);
    }

    private static bool? ParseBool(string text) => text.Trim().ToLowerInvariant() switch
    {
        "true" or "yes" or "1" => true,
        "false" or "no" or "0" => false,
        _ => null
    };

    private static string? FormatDecimal(decimal? value)
        => value?.ToString("0.####", CultureInfo.InvariantCulture);
}
