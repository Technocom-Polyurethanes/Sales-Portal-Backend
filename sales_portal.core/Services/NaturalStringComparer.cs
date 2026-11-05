namespace sales_portal.core.Services;

/// <summary>
/// Orders codes the way a person reads them: digit runs compare by value, so "2" comes before
/// "10" and "CUST-9" before "CUST-10". Letters compare without regard to case.
/// </summary>
public sealed class NaturalStringComparer : IComparer<string?>
{
    public static readonly NaturalStringComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var i = 0;
        var j = 0;

        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                var startX = i;
                var startY = j;
                while (i < x.Length && char.IsDigit(x[i]))
                {
                    i++;
                }

                while (j < y.Length && char.IsDigit(y[j]))
                {
                    j++;
                }

                var numberX = x.AsSpan(startX, i - startX).TrimStart('0');
                var numberY = y.AsSpan(startY, j - startY).TrimStart('0');

                if (numberX.Length != numberY.Length)
                {
                    return numberX.Length < numberY.Length ? -1 : 1;
                }

                var byDigits = numberX.CompareTo(numberY, StringComparison.Ordinal);
                if (byDigits != 0)
                {
                    return byDigits;
                }

                continue;
            }

            var charX = char.ToUpperInvariant(x[i]);
            var charY = char.ToUpperInvariant(y[j]);
            if (charX != charY)
            {
                return charX < charY ? -1 : 1;
            }

            i++;
            j++;
        }

        var remaining = (x.Length - i).CompareTo(y.Length - j);

        // Final tie-break keeps the order total, for example "7" and "007".
        return remaining != 0 ? remaining : string.CompareOrdinal(x, y);
    }
}
