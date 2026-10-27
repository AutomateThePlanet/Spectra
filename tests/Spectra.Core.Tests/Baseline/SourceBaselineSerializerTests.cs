using Spectra.Core.Baseline;

namespace Spectra.Core.Tests.Baseline;

/// <summary>Spec 084 (FR-007, T9 part): the baseline file is deterministic, local-diff friendly, and read strictly.</summary>
public class SourceBaselineSerializerTests
{
    private static readonly string H1 = "sha256:" + new string('1', 64);
    private static readonly string H2 = "sha256:" + new string('2', 64);
    private static readonly string H3 = "sha256:" + new string('3', 64);

    private static SourceBaseline Sample(bool reversed = false)
    {
        var baseline = new SourceBaseline();
        var ids = reversed ? new[] { "TC-2", "TC-10", "TC-1" } : ["TC-1", "TC-10", "TC-2"];
        foreach (var id in ids)
        {
            var entry = new BaselineEntry();
            var refs = reversed ? new[] { "docs/b.md", "docs/a.md#Über → x" } : ["docs/a.md#Über → x", "docs/b.md"];
            foreach (var r in refs) entry.Refs[r] = H1;
            entry.Criteria["AC-2"] = H2;
            entry.Criteria["AC-1"] = H3;
            baseline.Tests[id] = entry;
        }

        baseline.Tests["TC-3"] = new BaselineEntry();
        return baseline;
    }

    [Fact]
    public void RoundTrips()
    {
        var text = SourceBaselineSerializer.Serialize(Sample());
        Assert.Equal(text, SourceBaselineSerializer.Serialize(SourceBaselineSerializer.Parse(text, "f")));
    }

    [Fact]
    public void Output_IsSortedOrdinally_WhateverTheInsertionOrder() =>
        Assert.Equal(SourceBaselineSerializer.Serialize(Sample()), SourceBaselineSerializer.Serialize(Sample(reversed: true)));

    [Fact]
    public void Output_HasTheDocumentedFormat()
    {
        var text = SourceBaselineSerializer.Serialize(Sample());

        Assert.DoesNotContain('\r', text);
        Assert.EndsWith("}\n", text);
        Assert.False(text.EndsWith("\n\n", StringComparison.Ordinal));
        Assert.False(text.StartsWith('﻿'));
        Assert.StartsWith("{\n  \"version\": 1,\n  \"tests\": {\n    \"TC-1\": {\n      \"refs\": {", text);
        Assert.Contains("\"docs/a.md#Über → x\"", text);   // relaxed escaping
        Assert.Contains("\"TC-3\": {\n      \"refs\": {},\n      \"criteria\": {}\n    }", text);
        Assert.True(text.IndexOf("\"TC-10\"", StringComparison.Ordinal) < text.IndexOf("\"TC-2\"", StringComparison.Ordinal));
        Assert.True(text.IndexOf("\"AC-1\"", StringComparison.Ordinal) < text.IndexOf("\"AC-2\"", StringComparison.Ordinal));
    }

    [Fact]
    public void OneTestsChange_IsALocalDiff()
    {
        var before = SourceBaselineSerializer.Serialize(Sample()).Split('\n');
        var changed = Sample();
        changed.Tests["TC-10"].Refs["docs/b.md"] = H2;
        var after = SourceBaselineSerializer.Serialize(changed).Split('\n');

        Assert.Equal(before.Length, after.Length);
        var differing = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();
        Assert.Single(differing);
        Assert.Contains("docs/b.md", after[differing[0]]);
    }

    [Theory]
    [InlineData("{", "invalid JSON")]
    [InlineData("[]", "root must be an object")]
    [InlineData("{\"version\":2,\"tests\":{}}", "'version' must be 1")]
    [InlineData("{\"tests\":{}}", "'version' must be 1")]
    [InlineData("{\"version\":1,\"tests\":[]}", "'tests' must be an object")]
    [InlineData("{\"version\":1,\"tests\":{\"TC-1\":{\"criteria\":{}}}}", "'refs' object")]
    [InlineData("{\"version\":1,\"tests\":{\"TC-1\":{\"refs\":{},\"criteria\":{\"AC-1\":\"sha1:00\"}}}}", "fingerprint")]
    [InlineData("{\"version\":1,\"tests\":{\"TC-1\":{\"refs\":{\"a\":\"sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"},\"criteria\":{}}}}", "fingerprint")]
    [InlineData("{\"version\":1,\"tests\":{\"TC-1\":{\"refs\":{\"a\":1},\"criteria\":{}}}}", "fingerprint")]
    public void Parse_RejectsWhatIsNotABaseline_NamingTheFile(string json, string problem)
    {
        var ex = Assert.Throws<BaselineFormatException>(() => SourceBaselineSerializer.Parse(json, "test-cases/s/_source-baseline.json"));

        Assert.Contains("test-cases/s/_source-baseline.json", ex.Message);
        Assert.Contains(problem, ex.Message);
    }
}
