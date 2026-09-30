namespace Spectra.Core.Models;

/// <summary>
/// Spec 080 A3: the frontmatter keys the typed model reads, in the order the test file writer emits them. The one
/// definition used by both the parser (known vs unknown keys) and the writer (emission order), so a key can never
/// be known to one and missing from the other.
/// </summary>
public static class TestCaseFrontmatterKeys
{
    public static readonly IReadOnlyList<string> Ordered =
    [
        "id", "priority", "tags", "component", "description", "preconditions", "environment", "depends_on",
        "source_refs", "scenario_from_doc", "estimated_duration", "criteria", "requirements",
        "related_work_items", "bugs", "automated_by", "custom", "status", "orphaned_reason", "orphaned_date",
        "grounding"
    ];
}
