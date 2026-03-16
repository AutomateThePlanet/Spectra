using Spectra.Core.Models.Profile;
using Spectra.Core.Profile;

namespace Spectra.Core.Tests.Profile;

public sealed class ProfileContextBuilderTests
{
    private readonly ProfileContextBuilder _builder = new();

    [Fact]
    public void Build_DefaultProfile_ContainsBasicSections()
    {
        // Arrange
        var profile = EffectiveProfile.FromDefaults();

        // Act
        var context = _builder.Build(profile);

        // Assert
        Assert.Contains("Test Generation Profile", context);
        Assert.Contains("Detail Level", context);
        Assert.Contains("Formatting", context);
    }

    [Fact]
    public void Build_HighLevelDetail_ContainsBriefInstructions()
    {
        // Arrange
        var profile = CreateProfileWithDetailLevel(DetailLevel.HighLevel);

        // Act
        var context = _builder.Build(profile);

        // Assert
        Assert.Contains("High-Level", context);
        Assert.Contains("brief", context.ToLowerInvariant());
    }

    [Fact]
    public void Build_VeryDetailedLevel_ContainsGranularInstructions()
    {
        // Arrange
        var profile = CreateProfileWithDetailLevel(DetailLevel.VeryDetailed);

        // Act
        var context = _builder.Build(profile);

        // Assert
        Assert.Contains("Very Detailed", context);
        Assert.Contains("granular", context.ToLowerInvariant());
    }

    [Fact]
    public void Build_BulletFormat_ContainsBulletInstructions()
    {
        // Arrange
        var profile = CreateProfileWithFormatting(StepFormat.Bullets);

        // Act
        var context = _builder.Build(profile);

        // Assert
        Assert.Contains("bullet", context.ToLowerInvariant());
    }

    [Fact]
    public void Build_WithActionVerbs_ContainsActionVerbInstructions()
    {
        // Arrange
        var profile = CreateProfileWithFormatting(StepFormat.Numbered, useActionVerbs: true);

        // Act
        var context = _builder.Build(profile);

        // Assert
        Assert.Contains("action verb", context.ToLowerInvariant());
    }

    [Fact]
    public void Build_WithPaymentsDomain_ContainsPCIConsiderations()
    {
        // Arrange
        var profile = CreateProfileWithDomains([DomainType.Payments]);

        // Act
        var context = _builder.Build(profile);

        // Assert
        Assert.Contains("PCI", context);
        Assert.Contains("Payment", context);
    }

    [Fact]
    public void Build_WithPiiGdprDomain_ContainsDataProtection()
    {
        // Arrange
        var profile = CreateProfileWithDomains([DomainType.PiiGdpr]);

        // Act
        var context = _builder.Build(profile);

        // Assert
        Assert.Contains("GDPR", context);
        Assert.Contains("data protection", context.ToLowerInvariant());
    }

    [Fact]
    public void Build_WithExclusions_ContainsExclusionList()
    {
        // Arrange
        var profile = CreateProfileWithExclusions(["performance", "load_testing"]);

        // Act
        var context = _builder.Build(profile);

        // Assert
        Assert.Contains("Exclusions", context);
        Assert.Contains("Performance", context);
        Assert.Contains("Load testing", context);
    }

    [Fact]
    public void Build_WithMinNegativeScenarios_ContainsRequirement()
    {
        // Arrange
        var profile = CreateProfileWithMinNegative(5);

        // Act
        var context = _builder.Build(profile);

        // Assert
        Assert.Contains("5 negative", context.ToLowerInvariant());
    }

    [Fact]
    public void Build_WithHighPriority_ContainsPriorityInfo()
    {
        // Arrange
        var profile = CreateProfileWithPriority(Priority.High);

        // Act
        var context = _builder.Build(profile);

        // Assert
        Assert.Contains("High (P1)", context);
    }

    [Fact]
    public void BuildMinimal_DefaultProfile_ReturnsEmpty()
    {
        // Arrange
        var profile = EffectiveProfile.FromDefaults();

        // Act
        var context = _builder.BuildMinimal(profile);

        // Assert
        Assert.Empty(context);
    }

    [Fact]
    public void BuildMinimal_LoadedProfile_ReturnsSummary()
    {
        // Arrange
        var generationProfile = new GenerationProfile
        {
            Options = new ProfileOptions
            {
                DetailLevel = DetailLevel.VeryDetailed,
                DefaultPriority = Priority.High
            }
        };
        var profile = EffectiveProfile.FromProfile(
            generationProfile,
            ProfileSource.Repository("test.md"));

        // Act
        var context = _builder.BuildMinimal(profile);

        // Assert
        Assert.Contains("Profile", context);
        Assert.Contains("Very Detailed", context);
        Assert.Contains("High", context);
    }

    [Fact]
    public void Build_NullProfile_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _builder.Build(null!));
    }

    [Fact]
    public void Build_WithScreenshots_ContainsScreenshotInstructions()
    {
        // Arrange
        var generationProfile = new GenerationProfile
        {
            Options = new ProfileOptions
            {
                Formatting = new FormattingOptions { IncludeScreenshots = true }
            }
        };
        var profile = EffectiveProfile.FromProfile(
            generationProfile,
            ProfileSource.Repository("test.md"));

        // Act
        var context = _builder.Build(profile);

        // Assert
        Assert.Contains("screenshot", context.ToLowerInvariant());
    }

    [Fact]
    public void Build_WithMaxStepsPerTest_ContainsStepLimit()
    {
        // Arrange
        var generationProfile = new GenerationProfile
        {
            Options = new ProfileOptions
            {
                Formatting = new FormattingOptions { MaxStepsPerTest = 15 }
            }
        };
        var profile = EffectiveProfile.FromProfile(
            generationProfile,
            ProfileSource.Repository("test.md"));

        // Act
        var context = _builder.Build(profile);

        // Assert
        Assert.Contains("15", context);
        Assert.Contains("steps", context.ToLowerInvariant());
    }

    private static EffectiveProfile CreateProfileWithDetailLevel(DetailLevel level)
    {
        return EffectiveProfile.FromProfile(
            new GenerationProfile
            {
                Options = new ProfileOptions { DetailLevel = level }
            },
            ProfileSource.Repository("test.md"));
    }

    private static EffectiveProfile CreateProfileWithFormatting(StepFormat format, bool useActionVerbs = false)
    {
        return EffectiveProfile.FromProfile(
            new GenerationProfile
            {
                Options = new ProfileOptions
                {
                    Formatting = new FormattingOptions
                    {
                        StepFormat = format,
                        UseActionVerbs = useActionVerbs
                    }
                }
            },
            ProfileSource.Repository("test.md"));
    }

    private static EffectiveProfile CreateProfileWithDomains(IReadOnlyList<DomainType> domains)
    {
        return EffectiveProfile.FromProfile(
            new GenerationProfile
            {
                Options = new ProfileOptions
                {
                    Domain = new DomainOptions { Domains = domains }
                }
            },
            ProfileSource.Repository("test.md"));
    }

    private static EffectiveProfile CreateProfileWithExclusions(IReadOnlyList<string> exclusions)
    {
        return EffectiveProfile.FromProfile(
            new GenerationProfile
            {
                Options = new ProfileOptions { Exclusions = exclusions }
            },
            ProfileSource.Repository("test.md"));
    }

    private static EffectiveProfile CreateProfileWithMinNegative(int minNegative)
    {
        return EffectiveProfile.FromProfile(
            new GenerationProfile
            {
                Options = new ProfileOptions { MinNegativeScenarios = minNegative }
            },
            ProfileSource.Repository("test.md"));
    }

    private static EffectiveProfile CreateProfileWithPriority(Priority priority)
    {
        return EffectiveProfile.FromProfile(
            new GenerationProfile
            {
                Options = new ProfileOptions { DefaultPriority = priority }
            },
            ProfileSource.Repository("test.md"));
    }
}
