using System.Text.Json;
using System.Text.Json.Nodes;

namespace Spectra.CLI.Commands.Generate;

/// <summary>
/// Spec 097 (FR-017): the result JSON of a batch ingest, with <c>"dry_run": true</c> added when the run wrote nothing
/// because of <c>--dry-run</c>. Without it the JSON is exactly what it was before 097.
/// </summary>
internal static class DryRunJson
{
    /// <summary>Usage error (exit 1, plain stderr) for <c>ingest-grounding --test … --dry-run</c>.</summary>
    public const string RequiresAllMessage = "--dry-run requires --all";

    public static string Serialize(object payload, bool dryRun, JsonSerializerOptions? options = null)
    {
        if (!dryRun)
        {
            return JsonSerializer.Serialize(payload, options);
        }

        var node = JsonSerializer.SerializeToNode(payload, options)!.AsObject();
        node["dry_run"] = true;
        return node.ToJsonString(options);
    }
}
