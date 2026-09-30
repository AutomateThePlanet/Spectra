using Spectra.CLI.IO;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models;

namespace Spectra.CLI.Tests.IO;

/// <summary>
/// Spec 080 A3 (FR-003): the writer emits keys from the one shared list, and a file carrying every known key keeps
/// every key through a rewrite.
/// </summary>
public class TestFileWriterAllKeysTests
{
    [Fact]
    public void KeyOrder_IsTheSharedList()
    {
        Assert.Equal(TestCaseFrontmatterKeys.Ordered, TestFileWriter.KeyOrder);
    }

    [Fact]
    public void EveryKnownKey_SurvivesARewrite()
    {
        var original = FullFieldTestCase.Parse(FullFieldTestCase.Markdown);
        var before = original.Layout!.FrontmatterEntries.Where(e => e.IsKnown).Select(e => e.Key).ToHashSet();

        var reparsed = FullFieldTestCase.Parse(new TestFileWriter().FormatTestCase(original));
        var after = reparsed.Layout!.FrontmatterEntries.Where(e => e.IsKnown).Select(e => e.Key).ToHashSet();

        Assert.True(before.SetEquals(after), "Lost on rewrite: " + string.Join(", ", before.Except(after)));
        // The fixture carries every key except `preconditions` (its content lives in the body section).
        Assert.True(before.SetEquals(TestCaseFrontmatterKeys.Ordered.Where(k => k != "preconditions")),
            "Fixture lacks: " + string.Join(", ", TestCaseFrontmatterKeys.Ordered.Except(before)));
    }
}
