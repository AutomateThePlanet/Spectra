using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spectra.CLI.IO;

/// <summary>
/// Spec 103 (M1): a review recorded by <c>review-flagged --action feedback --fix</c>, waiting for the repaired test's
/// grounding block. The repair chain's <c>ingest-update</c> drops the old critic block (Spec 090) and the review record
/// in it; the final <c>ingest-grounding</c> writes this one back.
/// </summary>
public sealed record PendingReview
{
    [JsonPropertyName("reviewed_by")]
    public required string ReviewedBy { get; init; }

    [JsonPropertyName("reviewed_at")]
    public required DateTimeOffset ReviewedAt { get; init; }

    [JsonPropertyName("note")]
    public string? Note { get; init; }
}

/// <summary>
/// Spec 103 (M1): one staged review per test at <c>.spectra/reviews/&lt;suite&gt;/&lt;id&gt;.json</c>. Written by
/// <c>feedback --fix</c>, removed by any later review action on the test and once <c>ingest-grounding</c> applies it.
/// </summary>
public sealed class PendingReviewStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _root;

    public PendingReviewStore(string workingDir)
    {
        _root = Path.Combine(workingDir, ".spectra", "reviews");
    }

    public string PathFor(string suite, string testId) => Path.Combine(_root, suite, testId + ".json");

    public async Task SaveAsync(string suite, string testId, PendingReview review, CancellationToken ct = default)
    {
        var path = PathFor(suite, testId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(review, SerializerOptions), ct);
    }

    /// <summary>The staged review, or null when there is none or it can't be read.</summary>
    public async Task<PendingReview?> LoadAsync(string suite, string testId, CancellationToken ct = default)
    {
        var path = PathFor(suite, testId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PendingReview>(await File.ReadAllTextAsync(path, ct), SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Remove(string suite, string testId)
    {
        var path = PathFor(suite, testId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
