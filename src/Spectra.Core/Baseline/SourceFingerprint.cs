using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Spectra.Core.Text;

namespace Spectra.Core.Baseline;

/// <summary>
/// Spec 084 (FR-004, FR-006): <c>sha256:</c> plus 64 lowercase hex digits. A section's fingerprint is over its raw
/// text with a leading BOM removed and line endings normalised to LF, so a CRLF and an LF checkout of the same commit
/// agree, while a markup, whitespace or case change is still seen (COSMETIC is then decided from the snapshot). A
/// criterion's fingerprint is over the normalised text of its <c>text</c>.
/// </summary>
public static partial class SourceFingerprint
{
    public const string Prefix = "sha256:";

    public static string OfSection(string raw) => Of(SectionExtractor.NormalizeNewlines(raw));

    public static string OfCriterion(string text) => Of(TextNormalizer.Normalize(text));

    /// <summary>The 64 hex digits of a fingerprint (the snapshot file name).</summary>
    public static string Hex(string fingerprint) =>
        fingerprint.StartsWith(Prefix, StringComparison.Ordinal) ? fingerprint[Prefix.Length..] : fingerprint;

    public static bool IsValid(string? fingerprint) => fingerprint is not null && FormatRegex().IsMatch(fingerprint);

    private static string Of(string text) =>
        Prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [GeneratedRegex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatRegex();
}
