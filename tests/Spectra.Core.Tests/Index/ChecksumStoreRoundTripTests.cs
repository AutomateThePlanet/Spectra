using Spectra.Core.Index;
using Spectra.Core.Models.Index;

namespace Spectra.Core.Tests.Index;

public class ChecksumStoreRoundTripTests
{
    [Fact]
    public void Render_EmptyStore_RoundTripsClean()
    {
        var store = new ChecksumStore
        {
            Version = 2,
            GeneratedAt = new DateTimeOffset(2026, 4, 29, 15, 0, 0, TimeSpan.Zero),
            Checksums = new Dictionary<string, string>(),
        };

        var json = ChecksumStoreWriter.Render(store);
        var roundtrip = ChecksumStoreReader.Parse(json);

        Assert.Equal(2, roundtrip.Version);
        Assert.Empty(roundtrip.Checksums);
    }

    [Fact]
    public void Render_PopulatedStore_PreservesAllChecksums()
    {
        var store = new ChecksumStore
        {
            Version = 2,
            GeneratedAt = DateTimeOffset.UtcNow,
            Checksums = new Dictionary<string, string>
            {
                ["docs/a.md"] = new string('a', 64),
                ["docs/b.md"] = new string('b', 64),
            },
        };

        var json = ChecksumStoreWriter.Render(store);
        var roundtrip = ChecksumStoreReader.Parse(json);

        Assert.Equal(2, roundtrip.Checksums.Count);
        Assert.Equal(new string('a', 64), roundtrip.Checksums["docs/a.md"]);
        Assert.Equal(new string('b', 64), roundtrip.Checksums["docs/b.md"]);
    }

    [Fact]
    public void Parse_RejectsMalformedHexDigest()
    {
        var json = """
        {
          "version": 2,
          "generated_at": "2026-04-29T15:00:00+00:00",
          "checksums": {
            "docs/a.md": "not-a-real-hash"
          }
        }
        """;

        var ex = Assert.Throws<InvalidOperationException>(() => ChecksumStoreReader.Parse(json));
        Assert.Contains("hex digest", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_SortsKeysAlphabetically()
    {
        var store = new ChecksumStore
        {
            Version = 2,
            GeneratedAt = DateTimeOffset.UtcNow,
            Checksums = new Dictionary<string, string>
            {
                ["docs/zeta.md"] = new string('1', 64),
                ["docs/alpha.md"] = new string('2', 64),
                ["docs/mu.md"] = new string('3', 64),
            },
        };

        var json = ChecksumStoreWriter.Render(store);
        var alphaIdx = json.IndexOf("alpha", StringComparison.Ordinal);
        var muIdx = json.IndexOf("mu.md", StringComparison.Ordinal);
        var zetaIdx = json.IndexOf("zeta", StringComparison.Ordinal);

        Assert.True(alphaIdx > 0);
        Assert.True(alphaIdx < muIdx);
        Assert.True(muIdx < zetaIdx);
    }
}
