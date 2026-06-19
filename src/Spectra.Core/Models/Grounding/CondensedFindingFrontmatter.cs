using YamlDotNet.Serialization;

namespace Spectra.Core.Models.Grounding;

/// <summary>
/// YAML DTO for a condensed critic finding embedded in test frontmatter.
/// </summary>
public sealed class CondensedFindingFrontmatter
{
    [YamlMember(Alias = "element")]
    public string? Element { get; set; }

    [YamlMember(Alias = "reason")]
    public string? Reason { get; set; }
}
