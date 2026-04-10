namespace Spectra.Core.Models;

/// <summary>
/// A loaded prompt template with metadata and body text containing {{placeholder}} syntax.
/// </summary>
public sealed record PromptTemplate
{
    public required string SpectraVersion { get; init; }
    public required string TemplateId { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<PlaceholderSpec> Placeholders { get; init; }
    public required string Body { get; init; }
    public bool IsUserCustomized { get; init; }
}

/// <summary>
/// Declares a single placeholder used in a prompt template.
/// </summary>
public sealed record PlaceholderSpec
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}
