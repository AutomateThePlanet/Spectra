using YamlDotNet.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// An acceptance criterion extracted or imported into SPECTRA.
/// </summary>
public sealed class AcceptanceCriterion
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "text")]
    public string Text { get; set; } = string.Empty;

    [YamlMember(Alias = "rfc2119")]
    public string? Rfc2119 { get; set; }

    [YamlMember(Alias = "source")]
    public string? Source { get; set; }

    [YamlMember(Alias = "source_type")]
    public string SourceType { get; set; } = "document";

    [YamlMember(Alias = "source_doc")]
    public string? SourceDoc { get; set; }

    [YamlMember(Alias = "source_section")]
    public string? SourceSection { get; set; }

    [YamlMember(Alias = "component")]
    public string? Component { get; set; }

    [YamlMember(Alias = "priority")]
    public string Priority { get; set; } = "medium";

    [YamlMember(Alias = "tags")]
    public List<string> Tags { get; set; } = [];

    [YamlMember(Alias = "linked_test_ids")]
    public List<string> LinkedTestIds { get; set; } = [];
}

/// <summary>
/// Root document for criteria YAML files.
/// </summary>
public sealed class CriteriaDocument
{
    [YamlMember(Alias = "criteria")]
    public List<AcceptanceCriterion> Criteria { get; set; } = [];
}
