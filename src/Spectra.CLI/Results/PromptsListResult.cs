using System.Text.Json.Serialization;

namespace Spectra.CLI.Results;

public sealed class PromptsListResult : CommandResult
{
    [JsonPropertyName("templates")]
    public required IReadOnlyList<TemplateStatusEntry> Templates { get; init; }
}

public sealed class TemplateStatusEntry
{
    [JsonPropertyName("templateId")]
    public required string TemplateId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("filePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilePath { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }
}

public sealed class PromptsValidateResult : CommandResult
{
    [JsonPropertyName("templateId")]
    public required string TemplateId { get; init; }

    [JsonPropertyName("valid")]
    public bool Valid { get; init; }

    [JsonPropertyName("placeholders")]
    public int Placeholders { get; init; }

    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = [];

    [JsonPropertyName("errors")]
    public IReadOnlyList<string> Errors { get; init; } = [];
}
