using System.Text;

namespace SparkasseCsvMerger.Core;

/// <summary>
/// Maps header names from different Sparkasse export flavours onto one set of
/// canonical column names, so files with slightly different headers can be merged.
/// </summary>
internal static class ColumnResolver
{
    public static readonly IReadOnlyList<string> PreferredOrder = new[]
    {
        "Auftragskonto",
        "Buchungstag",
        "Valutadatum",
        "Buchungstext",
        "Verwendungszweck",
        "Gläubiger ID",
        "Empfänger/Zahlungspflichtiger",
        "Empfänger IBAN",
        "Empfänger BIC",
        "Betrag",
        "Währung",
        "Info",
        "Kategorie",
        "Mandatsreferenz",
        "Kundenreferenz (End-to-End)",
        "Sammlerreferenz",
        "Lastschrift Ursprungsbetrag",
        "Auslagenersatz Rücklastschrift"
    };

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["auftragskonto"] = "Auftragskonto",
        ["konto"] = "Auftragskonto",
        ["auftragskontonummer"] = "Auftragskonto",

        ["buchungstag"] = "Buchungstag",
        ["buchungsdatum"] = "Buchungstag",
        ["buchungstagdatum"] = "Buchungstag",

        ["valutadatum"] = "Valutadatum",
        ["valuta"] = "Valutadatum",
        ["wertstellung"] = "Valutadatum",
        ["wertstellungsdatum"] = "Valutadatum",

        ["buchungstext"] = "Buchungstext",
        ["umsatzart"] = "Buchungstext",
        ["buchungstextumsatzart"] = "Buchungstext",

        ["verwendungszweck"] = "Verwendungszweck",
        ["verwendungszweck1"] = "Verwendungszweck",

        ["glaeubigerid"] = "Gläubiger ID",
        ["glaeubigeridentifikationsnummer"] = "Gläubiger ID",
        ["creditorid"] = "Gläubiger ID",

        ["empfaengerzahlungspflichtiger"] = "Empfänger/Zahlungspflichtiger",
        ["empfaenger"] = "Empfänger/Zahlungspflichtiger",
        ["zahlungspflichtiger"] = "Empfänger/Zahlungspflichtiger",
        ["beguenstigterzahlungspflichtiger"] = "Empfänger/Zahlungspflichtiger",
        ["nameempfaenger"] = "Empfänger/Zahlungspflichtiger",

        ["empfaengeriban"] = "Empfänger IBAN",
        ["ibanempfaenger"] = "Empfänger IBAN",
        ["iban"] = "Empfänger IBAN",
        ["kontonummer"] = "Empfänger IBAN",
        ["kontonummeriban"] = "Empfänger IBAN",
        ["empfaengerkontonummer"] = "Empfänger IBAN",

        ["empfaengerbic"] = "Empfänger BIC",
        ["bicempfaenger"] = "Empfänger BIC",
        ["bic"] = "Empfänger BIC",
        ["swift"] = "Empfänger BIC",
        ["bicswiftcode"] = "Empfänger BIC",
        ["blz"] = "Empfänger BIC",
        ["empfaengerblz"] = "Empfänger BIC",

        ["betrag"] = "Betrag",
        ["umsatz"] = "Betrag",

        ["waehrung"] = "Währung",
        ["currency"] = "Währung",

        ["info"] = "Info",
        ["infozusatz"] = "Info",

        ["kategorie"] = "Kategorie",
        ["category"] = "Kategorie",

        ["mandatsreferenz"] = "Mandatsreferenz",
        ["kundenreferenz"] = "Kundenreferenz (End-to-End)",
        ["kundenreferenzendtoend"] = "Kundenreferenz (End-to-End)",
        ["sammlerreferenz"] = "Sammlerreferenz",
        ["lastschriftursprungsbetrag"] = "Lastschrift Ursprungsbetrag",
        ["auslagenersatzruecklastschrift"] = "Auslagenersatz Rücklastschrift"
    };

    public static string NormalizeKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var builder = new StringBuilder(raw.Length);

        foreach (char c in raw.Trim())
        {
            if (char.IsWhiteSpace(c))
                continue;

            switch (char.ToLowerInvariant(c))
            {
                case 'ä':
                    builder.Append("ae");
                    break;
                case 'ö':
                    builder.Append("oe");
                    break;
                case 'ü':
                    builder.Append("ue");
                    break;
                case 'ß':
                    builder.Append("ss");
                    break;
                case '.':
                case ':':
                case '-':
                case '_':
                case '/':
                case '(':
                case ')':
                case '*':
                case '"':
                case '\'':
                    break;
                default:
                    builder.Append(char.ToLowerInvariant(c));
                    break;
            }
        }

        return builder.ToString();
    }

    public static string Resolve(string header)
    {
        string trimmed = header.Trim();
        string key = NormalizeKey(trimmed);

        return Aliases.TryGetValue(key, out string? canonical) ? canonical : trimmed;
    }
}
