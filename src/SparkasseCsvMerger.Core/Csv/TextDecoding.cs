using System.Text;

namespace SparkasseCsvMerger.Core;

/// <summary>
/// Decodes CSV bytes. Bank exports are either UTF-8 (with or without BOM) or
/// Windows-1252/ISO-8859-1, so BOMs are honoured and otherwise strict UTF-8 is tried
/// before falling back to Windows-1252.
/// </summary>
internal static class TextDecoding
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static TextDecoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static (string Text, string EncodingName) Decode(byte[] bytes, string? forcedEncoding)
    {
        if (!string.IsNullOrWhiteSpace(forcedEncoding))
        {
            Encoding forced = Encoding.GetEncoding(forcedEncoding.Trim());
            return (TrimBom(forced.GetString(bytes)), Describe(forced));
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), "utf-8 (BOM)");

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), "utf-16 (BOM)");

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), "utf-16 BE (BOM)");

        try
        {
            return (StrictUtf8.GetString(bytes), "utf-8");
        }
        catch (DecoderFallbackException)
        {
            Encoding fallback = Encoding.GetEncoding(1252);
            return (fallback.GetString(bytes), "windows-1252");
        }
    }

    private static string TrimBom(string text) =>
        text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;

    private static string Describe(Encoding encoding) =>
        $"{encoding.WebName} (forced)";
}
