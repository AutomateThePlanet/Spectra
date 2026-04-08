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
}
