using YamlDotNet.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// An entry in the criteria master index pointing to a criteria file.
/// </summary>
public sealed class CriteriaSource
{
    [YamlMember(Alias = "file")]
    public string File { get; set; } = string.Empty;

    [YamlMember(Alias = "source_doc")]
    public string? SourceDoc { get; set; }

    [YamlMember(Alias = "source_type")]
    public string SourceType { get; set; } = "document";

    [YamlMember(Alias = "doc_hash")]
    public string? DocHash { get; set; }

    [YamlMember(Alias = "criteria_count")]
    public int CriteriaCount { get; set; }

    [YamlMember(Alias = "last_extracted")]
    public DateTime? LastExtracted { get; set; }

    [YamlMember(Alias = "imported_at")]
    public DateTime? ImportedAt { get; set; }

    /// <summary>
    /// Spec 048: extraction outcome that produced this entry. Always
    /// <c>"extracted"</c> for entries written by Spec 047+ code paths
    /// (only genuine extraction outcomes reach the cache). Reserved
    /// future values: <c>"empty_response"</c>, <c>"parse_failure"</c>.
    /// Legacy entries without this field deserialize as <c>"extracted"</c>
    /// via this property default — no migration step required.
    /// </summary>
    [YamlMember(Alias = "outcome")]
    public string Outcome { get; set; } = "extracted";
}
