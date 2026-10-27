using Spectra.Core.Validation;

namespace Spectra.Core.Tests.Validation;

/// <summary>
/// Spec 095 T1 (FR-001, FR-004; R1): one per-behaviour coverage rule — a behaviour is covered when an existing test's
/// title is at least the threshold similar (the duplicate detector's title measure, unchanged); <c>covered_by</c> is the
/// most similar test, ties to the lowest id (ordinal). On db20415 there was no per-behaviour rule.
/// </summary>
public class TitleCoverageTests
{
    private static readonly string[] Words =
    [
        "upload", "quota", "file", "share", "link", "expires", "after", "days", "folder", "nested", "trash", "restore",
        "version", "password", "locked", "minutes", "Viewer", "editor", "revoke", "session", "settings", "rounded",
        "decimal", "banner", "warning", "refused", "succeeds", "exactly", "larger", "than", "GB", "TB", "name", "rename",
    ];

    private static string RandomTitle(Random random)
    {
        var count = random.Next(2, 10);
        var words = Enumerable.Range(0, count).Select(_ => Words[random.Next(Words.Length)]);
        var title = string.Join(random.Next(4) == 0 ? "  " : " ", words);
        return random.Next(3) == 0 ? title + "!" : title;
    }

    [Fact]
    public void TheSimilarity_IsExactlyTheDuplicateDetectorsTitleMeasure()
    {
        var random = new Random(4711);
        var detector = new DuplicateDetector();
        for (var i = 0; i < 2000; i++)
        {
            var a = RandomTitle(random);
            var b = RandomTitle(random);

            var match = TitleCoverage.Match([a], [("TC-001", b)], threshold: 0.0)[0];

            Assert.NotNull(match);
            Assert.Equal(detector.CalculateTitleSimilarity(a, b), match.Similarity, 12);
        }
    }

    [Fact]
    public void ACoveredBehaviour_HasTheMostSimilarTest_WithItsSimilarity()
    {
        var detector = new DuplicateDetector();
        var tests = new[] { ("TC-101", "Share link expires after 30 days"), ("TC-102", "Share link expires after 7 days") };

        var match = TitleCoverage.Match(["Share link expires after 7 days!"], tests, 0.6)[0];

        Assert.Equal("TC-102", match!.TestId);
        Assert.Equal(1.0, match.Similarity);
        Assert.Equal(detector.CalculateTitleSimilarity("Share link expires after 7 days!", "Share link expires after 7 days"), match.Similarity);
    }

    [Fact]
    public void TheThreshold_DecidesCoverage()
    {
        const string behaviour = "Upload above the quota is refused";
        const string test = "Upload above quota is refused now";
        var similarity = new DuplicateDetector().CalculateTitleSimilarity(behaviour, test);
        Assert.InRange(similarity, 0.6, 0.9);

        Assert.NotNull(TitleCoverage.Match([behaviour], [("TC-101", test)], 0.6)[0]);
        Assert.Null(TitleCoverage.Match([behaviour], [("TC-101", test)], 0.9)[0]);
    }

    [Fact]
    public void Ties_GoToTheLowestId()
    {
        var tests = new[] { ("TC-120", "Quota shown in settings"), ("TC-105", "Quota shown in settings") };

        var match = TitleCoverage.Match(["Quota shown in settings"], tests, 0.6)[0];

        Assert.Equal("TC-105", match!.TestId);
    }

    [Fact]
    public void Ties_UseOrdinalOrder_SoUnpaddedTC10ComesBeforeTC9()
    {
        var tests = new[] { ("TC-9", "Quota shown in settings"), ("TC-10", "Quota shown in settings") };

        var match = TitleCoverage.Match(["Quota shown in settings"], tests, 0.6)[0];

        Assert.Equal("TC-10", match!.TestId);
    }

    [Theory]
    [InlineData("", "Quota shown in settings")]
    [InlineData("Quota shown in settings", "")]
    [InlineData("   ", "Quota shown in settings")]
    [InlineData("!!!", "Quota shown in settings")]
    public void AnEmptyTitle_IsNeverCovered(string behaviour, string test)
    {
        Assert.Null(TitleCoverage.Match([behaviour], [("TC-101", test)], 0.0)[0]);
    }

    [Fact]
    public void CaseSpacingAndPunctuation_DontMatter()
    {
        var match = TitleCoverage.Match(["QUOTA   shown, in settings."], [("TC-101", "quota shown in settings")], 0.99)[0];

        Assert.Equal(1.0, match!.Similarity);
    }

    [Fact]
    public void NoTests_CoverNothing()
    {
        Assert.All(TitleCoverage.Match(["a", "b"], [], 0.6), m => Assert.Null(m));
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(-0.5, true)]
    [InlineData(1.5, true)]
    [InlineData(0.6, false)]
    [InlineData(1.0, false)]
    public void AThresholdOutsideZeroToOne_Warns(double threshold, bool warns)
    {
        var warning = TitleCoverage.ThresholdWarning(threshold); // Spec 098 F9: the default key is coverage_threshold

        if (warns)
        {
            Assert.Equal(
                $"generation.coverage_threshold = {threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)} is outside (0, 1]; "
                + "every behaviour (≤ 0) or none (> 1) is treated as covered.", warning);
        }
        else
        {
            Assert.Null(warning);
        }
    }
}
