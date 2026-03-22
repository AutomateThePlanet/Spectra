using Spectra.Core.Models.Grounding;
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

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

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

    [YamlMember(Alias = "scenario_from_doc")]
    public string? ScenarioFromDoc { get; set; }

    [YamlMember(Alias = "related_work_items")]
    public List<string> RelatedWorkItems { get; set; } = [];

    [YamlMember(Alias = "custom")]
    public Dictionary<string, object>? Custom { get; set; }

    [YamlMember(Alias = "grounding")]
    public GroundingFrontmatter? Grounding { get; set; }

    [YamlMember(Alias = "automated_by")]
    public List<string> AutomatedBy { get; set; } = [];

    [YamlMember(Alias = "requirements")]
    public List<string> Requirements { get; set; } = [];

    [YamlMember(Alias = "bugs")]
    public List<string> Bugs { get; set; } = [];
}
