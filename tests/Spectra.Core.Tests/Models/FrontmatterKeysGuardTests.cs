using System.Reflection;
using Spectra.Core.Models;
using Spectra.Core.Parsing;
using YamlDotNet.Serialization;

namespace Spectra.Core.Tests.Models;

/// <summary>
/// Spec 080 A3 (FR-003): the known frontmatter keys have one definition. A key the DTO reads but the list lacks
/// (or the other way round) would be silently dropped on every rewrite, so this fails the build's tests instead.
/// </summary>
public class FrontmatterKeysGuardTests
{
    [Fact]
    public void OrderedKeys_MatchTheDtoYamlAliases()
    {
        var aliases = typeof(TestCaseFrontmatter)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.GetCustomAttribute<YamlMemberAttribute>()?.Alias)
            .Where(a => a is not null)
            .Select(a => a!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(aliases.SetEquals(TestCaseFrontmatterKeys.Ordered),
            "In the DTO only: " + string.Join(", ", aliases.Except(TestCaseFrontmatterKeys.Ordered)) +
            "; in the key list only: " + string.Join(", ", TestCaseFrontmatterKeys.Ordered.Except(aliases)));
    }

    [Fact]
    public void OrderedKeys_HaveNoDuplicates()
    {
        Assert.Equal(TestCaseFrontmatterKeys.Ordered.Count, TestCaseFrontmatterKeys.Ordered.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Parser_KnownKeys_AreTheOrderedKeys()
    {
        Assert.True(TestCaseParser.KnownFrontmatterKeys.SetEquals(TestCaseFrontmatterKeys.Ordered));
        Assert.Equal(TestCaseFrontmatterKeys.Ordered.Count, TestCaseParser.KnownFrontmatterKeys.Count);
    }
}
