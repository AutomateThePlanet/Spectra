using YamlDotNet.Serialization;

namespace Spectra.Core.Models;

/// <summary>
/// DTO for YAML frontmatter deserialization.
/// Use TestCase for business logic.
/// </summary>
public sealed class TestCaseFrontmatter
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "priority")]
    public string Priority { get; set; } = string.Empty;

    [YamlMember(Alias = "tags")]
    public List<string> Tags { get; set; } = [];

    [YamlMember(Alias = "component")]
    public string? Component { get; set; }

    [YamlMember(Alias = "preconditions")]
    public string? Preconditions { get; set; }

    [YamlMember(Alias = "environment")]
    public List<string> Environment { get; set; } = [];

    [YamlMember(Alias = "estimated_duration")]
    public string? EstimatedDuration { get; set; }

    [YamlMember(Alias = "depends_on")]
    public string? DependsOn { get; set; }

    [YamlMember(Alias = "source_refs")]
    public List<string> SourceRefs { get; set; } = [];

    [YamlMember(Alias = "related_work_items")]
    public List<string> RelatedWorkItems { get; set; } = [];

    [YamlMember(Alias = "custom")]
    public Dictionary<string, object>? Custom { get; set; }
}
