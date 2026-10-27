namespace Spectra.Core.Text;

/// <summary>
/// The newline style of a text, as the 078 lossless writer detects it: CRLF when the first line break is preceded by a
/// carriage return, else LF. Spec 090 (FR-D3) reuses it so appends to a user's file keep its style.
/// </summary>
public static class Newlines
{
    public static string Detect(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lineBreak = text.IndexOf('\n');
        return lineBreak > 0 && text[lineBreak - 1] == '\r' ? "\r\n" : "\n";
    }
}
