using YamlDotNet.Serialization;

namespace Spectra.Core.Models.Coverage;

/// <summary>
/// A requirement definition from _requirements.yaml.
/// </summary>
public sealed class RequirementDefinition
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "title")]
    public string Title { get; set; } = string.Empty;

    [YamlMember(Alias = "source")]
    public string? Source { get; set; }

    [YamlMember(Alias = "priority")]
    public string? Priority { get; set; }
}

/// <summary>
/// Root document for _requirements.yaml.
/// </summary>
public sealed class RequirementsDocument
{
    [YamlMember(Alias = "requirements")]
    public List<RequirementDefinition> Requirements { get; set; } = [];
}
