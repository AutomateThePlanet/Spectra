using System.Text.Json;
using Spectra.Core.Models;

namespace Spectra.Integration.Tests.Support;

/// <summary>
/// Spec 052: builds the index-loader delegates the MCP tools require by reading
/// the REAL <c>test-cases/{suite}/_index.json</c> the generation flow produced.
/// Reading the on-disk file (not an in-memory stub) is what proves Spec 049's
/// index-registration end to end — the MCP tools see a from-description test
/// only because it was actually written to the index.
/// </summary>
public static class OnDiskIndexLoader
{
    private static readonly JsonSerializerOptions Options =
        new() { PropertyNameCaseInsensitive = true };

    public static Func<string, IEnumerable<TestIndexEntry>> For(string testsPath)
        => suite =>
        {
            var indexPath = Path.Combine(testsPath, suite, "_index.json");
            if (!File.Exists(indexPath))
                return Array.Empty<TestIndexEntry>();

            var index = JsonSerializer.Deserialize<MetadataIndex>(File.ReadAllText(indexPath), Options);
            return index?.Tests ?? (IReadOnlyList<TestIndexEntry>)Array.Empty<TestIndexEntry>();
        };

    public static Func<IEnumerable<string>> SuiteList(string testsPath)
        => () => Directory.Exists(testsPath)
            ? Directory.GetDirectories(testsPath).Select(d => Path.GetFileName(d)!).ToList()
            : new List<string>();
}
