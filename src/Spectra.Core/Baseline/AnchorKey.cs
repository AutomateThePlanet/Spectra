using System.Text;

namespace Spectra.Core.Baseline;

/// <summary>
/// Spec 084 (D2): the tolerant comparison key for a source-ref anchor and a heading's text. Lowercase (invariant),
/// letters and digits only, so GitBook (<c>#Create-customer-records</c>), GitHub (<c>#create-customer-records</c>) and
/// generator-style anchors all match the heading "Create customer records". Unicode letters count as letters, and
/// diacritics are kept: <c>#über</c> matches "Über", <c>#uber</c> does not.
/// </summary>
public static class AnchorKey
{
    /// <summary>The key of a heading's text (or any text).</summary>
    public static string Make(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>The key of an anchor as written in a ref: percent-escapes are decoded first (a malformed escape is kept).</summary>
    public static string FromAnchor(string? anchor)
    {
        if (string.IsNullOrEmpty(anchor))
        {
            return string.Empty;
        }

        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(anchor);
        }
        catch (UriFormatException)
        {
            decoded = anchor;
        }

        return Make(decoded);
    }
}
