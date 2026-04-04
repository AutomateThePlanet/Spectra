using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class DashboardResult : CommandResult
{
    [JsonPropertyName("output_path")]
    public required string OutputPath { get; init; }

    [JsonPropertyName("pages_generated")]
    public int PagesGenerated { get; init; }

    [JsonPropertyName("suites_included")]
    public int SuitesIncluded { get; init; }

    [JsonPropertyName("tests_included")]
    public int TestsIncluded { get; init; }

    [JsonPropertyName("runs_included")]
    public int RunsIncluded { get; init; }
}
