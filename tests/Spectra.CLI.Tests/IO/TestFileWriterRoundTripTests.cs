using System.Globalization;
using Spectra.CLI.IO;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models;
using Spectra.Core.Models.Grounding;

namespace Spectra.CLI.Tests.IO;

/// <summary>
/// Spec 078: writing a parsed test is lossless (contracts/test-file-format.md G1–G6).
/// </summary>
public class TestFileWriterRoundTripTests
{
    private readonly TestFileWriter _writer = new();

    private static TestCase Parse(string markdown) => FullFieldTestCase.Parse(markdown);

    [Fact]
    public void G1_FullFieldFile_ParseWriteParse_IsEquivalent()
    {
        var original = Parse(FullFieldTestCase.Markdown);
        var reparsed = Parse(_writer.FormatTestCase(original));

        // Values are identical. The layout keeps every entry and segment; only the unknown keys move behind
        // the known ones on this first (normalising) write — order stability is G2.
        TestCaseEquivalence.AssertEquivalent(original, reparsed, includeLayout: false);
        Assert.Equal(original.Layout!.FrontmatterEntries.OrderBy(e => e.Key), reparsed.Layout!.FrontmatterEntries.OrderBy(e => e.Key));
        Assert.Equal(original.Layout.BodySegments, reparsed.Layout.BodySegments);
    }

    [Fact]
    public void G1_CrlfFile_ParseWriteParse_IsEquivalent()
    {
        var original = Parse(FullFieldTestCase.CrlfMarkdown);
        var reparsed = Parse(_writer.FormatTestCase(original));

        // Values are identical. The layout keeps every entry and segment; only the unknown keys move behind
        // the known ones on this first (normalising) write — order stability is G2.
        TestCaseEquivalence.AssertEquivalent(original, reparsed, includeLayout: false);
        Assert.Equal(original.Layout!.FrontmatterEntries.OrderBy(e => e.Key), reparsed.Layout!.FrontmatterEntries.OrderBy(e => e.Key));
        Assert.Equal(original.Layout.BodySegments, reparsed.Layout.BodySegments);
    }

    [Fact]
    public void G1_EveryFieldIsReadFromTheFixture()
    {
        var t = Parse(FullFieldTestCase.Markdown);

        Assert.Equal("Verifies payment: card declined path", t.Description);
        Assert.Equal(["staging", "chrome"], t.Environment);
        Assert.Equal(new TimeSpan(1, 30, 15), t.EstimatedDuration);
        Assert.Equal(["AB#1234"], t.RelatedWorkItems);
        Assert.Equal(["tests/e2e/checkout.spec.ts"], t.AutomatedBy);
        Assert.Equal(["REQ-12"], t.Requirements);
        Assert.Equal(["BUG-77"], t.Bugs);
        Assert.NotNull(t.Custom);
        Assert.Equal("orphaned", t.Status);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), t.OrphanedDate);
        Assert.Equal("manual-review", t.Grounding!.Source);
        Assert.Equal("vik", t.Grounding.CreatedBy);
        Assert.Equal("Checked against gateway spec", t.Grounding.Note);
    }

    [Fact]
    public void G2_WritingTwice_IsByteIdentical()
    {
        var first = _writer.FormatTestCase(Parse(FullFieldTestCase.Markdown));
        var second = _writer.FormatTestCase(Parse(first));

        Assert.Equal(first, second);
    }

    [Fact]
    public void G2_LayoutlessTest_WritingTwice_IsByteIdentical()
    {
        var inMemory = new TestCase
        {
            Id = "TC-7",
            FilePath = "TC-7.md",
            Priority = Priority.Low,
            Title = "In memory",
            Steps = ["a", "b"],
            ExpectedResult = "ok",
            Tags = ["x"]
        };

        var first = _writer.FormatTestCase(inMemory);
        var second = _writer.FormatTestCase(Parse(first));

        Assert.Equal(first, second);
    }

    [Fact]
    public void G3_UnmodifiedCanonicalFile_IsReproducedExactly()
    {
        var canonical = FullFieldTestCase.CanonicalMarkdown;

        Assert.Equal(canonical, _writer.FormatTestCase(Parse(canonical)));
    }

    [Fact]
    public void G3_UnmodifiedFullFieldFile_OnlyReordersUnknownKeys()
    {
        var output = _writer.FormatTestCase(Parse(FullFieldTestCase.Markdown));

        // Same multiset of lines: nothing lost, nothing re-rendered.
        Assert.Equal(
            FullFieldTestCase.Markdown.Split('\n').Order(),
            output.Split('\n').Order());
    }

    [Fact]
    public void G4_UnknownKeys_AreEmittedVerbatimAfterKnownKeys()
    {
        var output = _writer.FormatTestCase(Parse(FullFieldTestCase.Markdown));
        var frontmatter = output.Split("---\n")[1];

        var groundingIndex = frontmatter.IndexOf("grounding:", StringComparison.Ordinal);
        var ownerIndex = frontmatter.IndexOf("owner: qa-team\n", StringComparison.Ordinal);
        var ticketIndex = frontmatter.IndexOf("x_ticket: {id: 42, system: jira}\n", StringComparison.Ordinal);

        Assert.True(groundingIndex >= 0 && ownerIndex > groundingIndex, "owner must follow the known keys");
        Assert.True(ticketIndex > ownerIndex, "unknown keys keep their original relative order");
    }

    [Fact]
    public void G4_UnknownSection_StaysInPlace_WhenStepsChange()
    {
        var original = Parse(FullFieldTestCase.Markdown);
        var edited = original with { Steps = ["Open checkout", "Pay", "See error"] };

        var output = _writer.FormatTestCase(edited);

        var steps = output.IndexOf("## Steps", StringComparison.Ordinal);
        var notes = output.IndexOf("## Notes", StringComparison.Ordinal);
        var expected = output.IndexOf("## Expected Result", StringComparison.Ordinal);
        Assert.True(steps < notes && notes < expected, "## Notes stays between Steps and Expected Result");
        Assert.Contains("Hand-written notes the parser ignores.", output);
        Assert.Contains("1. Open checkout\n2. Pay\n3. See error\n", output);
        Assert.DoesNotContain("use card 4000", output); // the sub-bullet belonged to the replaced Steps

        TestCaseEquivalence.AssertEquivalent(edited, Parse(output), includeLayout: false);
    }

    [Fact]
    public void G4_UnchangedSteps_KeepSubBullet()
    {
        var output = _writer.FormatTestCase(Parse(FullFieldTestCase.Markdown));

        Assert.Contains("   - use card 4000 0000 0000 0002\n", output);
        Assert.Contains("Some intro text that no parser field captures.", output);
    }

    [Fact]
    public void G4_NewPreconditions_AreInsertedBeforeSteps()
    {
        const string md = "---\nid: TC-1\npriority: low\n---\n\n# T\n\n## Steps\n\n1. a\n\n## Notes\n\nn\n\n## Expected Result\n\nok\n";
        var edited = Parse(md) with { Preconditions = "Logged in" };

        var output = _writer.FormatTestCase(edited);

        Assert.True(output.IndexOf("## Preconditions", StringComparison.Ordinal) < output.IndexOf("## Steps", StringComparison.Ordinal));
        Assert.Equal("Logged in", Parse(output).Preconditions);
        Assert.Equal(output, _writer.FormatTestCase(Parse(output)));
    }

    [Fact]
    public void G5_CrlfInput_GivesCrlfOutput()
    {
        var output = _writer.FormatTestCase(Parse(FullFieldTestCase.CrlfMarkdown));

        Assert.DoesNotContain("\n", output.Replace("\r\n", string.Empty));
        Assert.Equal(FullFieldTestCase.CrlfMarkdown.Split("\r\n").Order(), output.Split("\r\n").Order());
    }

    [Fact]
    public void G5_LayoutlessTest_UsesLf()
    {
        var output = _writer.FormatTestCase(new TestCase
        {
            Id = "TC-1", FilePath = "TC-1.md", Priority = Priority.High, Title = "T", ExpectedResult = "ok"
        });

        Assert.DoesNotContain("\r", output);
    }

    [Theory]
    [InlineData("a: b")]
    [InlineData("x #y")]
    [InlineData("- lead")]
    [InlineData("[br")]
    [InlineData("*star")]
    [InlineData("&amp")]
    [InlineData("!bang")]
    [InlineData("|pipe")]
    [InlineData(">gt")]
    [InlineData("%pct")]
    [InlineData("@at")]
    [InlineData("`tick")]
    [InlineData("\"quote\"")]
    [InlineData("'single'")]
    [InlineData(@"C:\path\to")]
    [InlineData("line1\r\nline2")]
    [InlineData("line1\nline2")]
    [InlineData("~")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("5")]
    [InlineData("  padded  ")]
    public void G6_ChangedValues_RoundTripExactly(string value)
    {
        var original = Parse(FullFieldTestCase.Markdown);
        var edited = original with
        {
            Component = value,
            Tags = [value, "plain"],
            SourceRefs = [value],
            ScenarioFromDoc = value,
            Description = value,
            DependsOn = value
        };

        var reparsed = Parse(_writer.FormatTestCase(edited));

        Assert.Equal(value, reparsed.Component);
        Assert.Equal([value, "plain"], reparsed.Tags);
        Assert.Equal([value], reparsed.SourceRefs);
        Assert.Equal(value, reparsed.ScenarioFromDoc);
        Assert.Equal(value, reparsed.Description);
        Assert.Equal(value, reparsed.DependsOn);
    }

    [Theory]
    [InlineData("step: with colon")]
    [InlineData("step with # hash")]
    [InlineData(@"type C:\temp")]
    public void G6_ChangedStep_RoundTrips(string step)
    {
        var edited = Parse(FullFieldTestCase.Markdown) with { Steps = [step, "second"] };

        Assert.Equal([step, "second"], Parse(_writer.FormatTestCase(edited)).Steps);
    }

    [Fact]
    public void G6_Grounding_UsesInvariantCultureAndUtc()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var edited = Parse(FullFieldTestCase.Markdown) with
            {
                Grounding = new GroundingMetadata
                {
                    Verdict = VerificationVerdict.Grounded,
                    Score = 0.873,
                    Generator = "g",
                    Critic = "c",
                    VerifiedAt = new DateTimeOffset(2026, 3, 4, 7, 8, 9, 120, TimeSpan.FromHours(2))
                }
            };

            var output = _writer.FormatTestCase(edited);

            Assert.Contains("  score: 0.873\n", output);
            Assert.Contains("  verified_at: 2026-03-04T05:08:09.12Z\n", output);
            Assert.Equal(edited.Grounding!.VerifiedAt, Parse(output).Grounding!.VerifiedAt);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void G6_Grounding_WholeSecondsHaveNoFraction_AndTwoDecimalScoreIsKept()
    {
        var edited = Parse(FullFieldTestCase.Markdown) with
        {
            Grounding = new GroundingMetadata
            {
                Verdict = VerificationVerdict.Grounded,
                Score = 0.9,
                Generator = "g",
                Critic = "c",
                VerifiedAt = new DateTimeOffset(2026, 3, 4, 5, 8, 9, TimeSpan.Zero)
            }
        };

        var output = _writer.FormatTestCase(edited);

        Assert.Contains("  score: 0.90\n", output);
        Assert.Contains("  verified_at: 2026-03-04T05:08:09Z\n", output);
    }

    [Fact]
    public void Duration_KeepsSeconds()
    {
        var edited = Parse(FullFieldTestCase.Markdown) with { EstimatedDuration = TimeSpan.FromSeconds(90) };

        var output = _writer.FormatTestCase(edited);

        Assert.Contains("estimated_duration: 1m30s\n", output);
        Assert.Equal(TimeSpan.FromSeconds(90), Parse(output).EstimatedDuration);
    }

    [Fact]
    public void LayoutlessTest_WritesEmptyCriteria_AndFixedKeyOrder()
    {
        var output = _writer.FormatTestCase(new TestCase
        {
            Id = "TC-1",
            FilePath = "TC-1.md",
            Priority = Priority.High,
            Title = "T",
            ExpectedResult = "ok",
            Tags = ["a"],
            Component = "c",
            Description = "d",
            AutomatedBy = ["x.cs"],
            Bugs = ["B-1"]
        });

        Assert.Contains("criteria: []\n", output);
        var keys = output.Split("---\n")[1].Split('\n')
            .Where(l => l.Length > 0 && !l.StartsWith(' '))
            .Select(l => l[..l.IndexOf(':')])
            .ToList();
        var expectedOrder = TestFileWriter.KeyOrder.Where(keys.Contains).ToList();
        Assert.Equal(expectedOrder, keys);
    }

    [Fact]
    public void UnchangedFileWithoutCriteria_DoesNotGainCriteriaKey()
    {
        const string md = "---\nid: TC-1\npriority: low\n---\n\n# T\n\n## Steps\n\n1. a\n\n## Expected Result\n\nok\n";

        var output = _writer.FormatTestCase(Parse(md));

        Assert.Equal(md, output);
    }

    [Fact]
    public void UnchangedFlowStyleTags_AreKept()
    {
        const string md = "---\nid: TC-1\npriority: low\ntags: [a, b]\n---\n\n# T\n\n## Steps\n\n1. a\n\n## Expected Result\n\nok\n";

        Assert.Equal(md, _writer.FormatTestCase(Parse(md)));
    }

    [Fact]
    public void UnchangedInvalidPriority_IsKept()
    {
        const string md = "---\nid: TC-1\npriority: urgent\n---\n\n# T\n\n## Steps\n\n1. a\n\n## Expected Result\n\nok\n";

        Assert.Equal(md, _writer.FormatTestCase(Parse(md)));
    }

    [Fact]
    public void ChangingOnlyGrounding_ChangesOnlyGroundingLines()
    {
        var original = Parse(FullFieldTestCase.Markdown);
        var before = _writer.FormatTestCase(original);
        var after = _writer.FormatTestCase(original with
        {
            Grounding = new GroundingMetadata
            {
                Verdict = VerificationVerdict.Grounded, Score = 1.0, Generator = "g", Critic = "c",
                VerifiedAt = new DateTimeOffset(2026, 5, 5, 0, 0, 0, TimeSpan.Zero)
            }
        });

        static string[] WithoutGrounding(string text)
        {
            var lines = text.Split('\n').ToList();
            var start = lines.IndexOf("grounding:");
            var end = start + 1;
            while (end < lines.Count && lines[end].StartsWith(' ')) end++;
            lines.RemoveRange(start, end - start);
            return lines.ToArray();
        }

        Assert.Equal(WithoutGrounding(before), WithoutGrounding(after));
    }
}
