using YamlDotNet.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// Master index file (_criteria_index.yaml) tracking all criteria sources.
/// </summary>
public sealed class CriteriaIndex
{
    [YamlMember(Alias = "version")]
    public int Version { get; set; } = 1;

    [YamlMember(Alias = "total_criteria")]
    public int TotalCriteria { get; set; }

    [YamlMember(Alias = "sources")]
    public List<CriteriaSource> Sources { get; set; } = [];

    /// <summary>
    /// Spec 079: per ID prefix (e.g. <c>AC-CHECKOUT</c>), the highest number ever issued. Committed with the
    /// index so a removed criterion's number is never issued again, even in a fresh clone. Only increases.
    /// Null (omitted) until the first allocation; written sorted by prefix.
    /// </summary>
    [YamlMember(Alias = "id_high_water_marks")]
    public Dictionary<string, int>? IdHighWaterMarks { get; set; }
}
