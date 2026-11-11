using sales_portal.domain.Enums;

namespace sales_portal.core.Calculations;

/// <summary>
/// The terms and conditions printed on every offer. They are fixed on purpose: an offer cannot add,
/// remove or reword them. Keep the wording in sync with the frontend PDF templates (src/pdf/templates).
/// </summary>
public static class OfferTerms
{
    private static readonly string[] Arabic =
    {
        "برجاء إرسال مندوبكم بتفويض معتمد ومختوم لمصانعنا بمدينة بدر لإستلام الخامات فى المواعيد المحددة.",
        "تنطبق شروط القوى القهرية على العرض بعاليه."
    };

    private static readonly string[] English =
    {
        "Please send your representative, holding an authorised and stamped letter, to our factories in Badr City to collect the materials on the agreed dates.",
        "Force majeure conditions apply to this offer."
    };

    public static List<string> For(DocumentLanguage language)
        => (language == DocumentLanguage.Ar ? Arabic : English).ToList();
}
