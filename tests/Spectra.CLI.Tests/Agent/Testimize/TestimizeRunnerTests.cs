using Spectra.CLI.Agent.Testimize;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Testimize;
using Testimize.OutputGenerators;
using Testimize.Usage;

namespace Spectra.CLI.Tests.Agent.Testimize;

/// <summary>
/// v1.48.3: unit tests for the in-process Testimize orchestration layer.
/// Uses the library's default seed (42) so generated counts are deterministic.
/// </summary>
public class TestimizeRunnerTests
{
    [Fact]
    public void Generate_Disabled_ReturnsNullWithoutInvokingEngine()
    {
        var result = TestimizeRunner.Generate(
            fieldSpecs: [new FieldSpec { Name = "age", Type = "Integer", Min = 1, Max = 10 }],
            config: new TestimizeConfig { Enabled = false },
            fallbackDocs: null,
            suiteName: "test");

        Assert.Null(result);
    }

    [Fact]
    public void Generate_EmptySpecsAndNoFallback_ReturnsNullWithSkipLog()
    {
        var result = TestimizeRunner.Generate(
            fieldSpecs: [],
            config: new TestimizeConfig { Enabled = true },
            fallbackDocs: null,
            suiteName: "button suite");

        Assert.Null(result);
    }

    [Fact]
    public void Generate_EmptySpecsWithRegexFallback_RecoversFromDocs()
    {
        var docs = new List<SourceDocument>
        {
            new()
            {
                Path = "form.md",
                Title = "Form",
                Content = "The username must be 3 to 20 characters. Age must be between 18 and 100.",
                Sections = []
            }
        };

        var result = TestimizeRunner.Generate(
            fieldSpecs: [],
            config: new TestimizeConfig { Enabled = true, Strategy = "Pairwise" },
            fallbackDocs: docs,
            suiteName: "form");

        Assert.NotNull(result);
        Assert.True(result!.FieldCount >= 1);
        Assert.NotEmpty(result.TestCases);
    }

    [Fact]
    public void Generate_SingleField_SkipsWithInsufficientFieldsReason()
    {
        var specs = new List<FieldSpec>
        {
            new() { Name = "age", Type = "Integer", Min = 18, Max = 100, Required = true }
        };

        var result = TestimizeRunner.Generate(
            specs,
            new TestimizeConfig { Enabled = true, Strategy = "Pairwise" },
            fallbackDocs: null,
            suiteName: "registration");

        // Testimize's generators require ≥ 2 parameters. Runner skips
        // cleanly rather than letting the library throw.
        Assert.Null(result);
    }

    [Fact]
    public void Generate_IntegerAndEmailFields_ProducesTestCasesWithBoundaryValues()
    {
        var specs = new List<FieldSpec>
        {
            new() { Name = "age", Type = "Integer", Min = 18, Max = 100, Required = true },
            new() { Name = "email", Type = "Email", Required = true, MinLength = 6, MaxLength = 254 }
        };

        var result = TestimizeRunner.Generate(
            specs,
            new TestimizeConfig { Enabled = true, Strategy = "HybridArtificialBeeColony" },
            fallbackDocs: null,
            suiteName: "registration");

        Assert.NotNull(result);
        Assert.Equal(2, result!.FieldCount);
        Assert.NotEmpty(result.TestCases);
        Assert.All(result.TestCases, row => Assert.Equal(2, row.Values.Count));
        Assert.Contains(result.TestCases, row => row.Values[0].FieldName == "age");
    }

    [Fact]
    public void Generate_DateAndIntegerFields_ParsesIsoBoundsAndProducesTestCases()
    {
        var specs = new List<FieldSpec>
        {
            new() { Name = "birth", Type = "Date", MinDate = "1920-01-01", MaxDate = "2010-12-31" },
            new() { Name = "age", Type = "Integer", Min = 0, Max = 120 }
        };

        var result = TestimizeRunner.Generate(
            specs,
            new TestimizeConfig { Enabled = true, Strategy = "HybridArtificialBeeColony" },
            fallbackDocs: null,
            suiteName: "date-test");

        Assert.NotNull(result);
        Assert.Equal("HybridArtificialBeeColony", result!.Strategy);
        Assert.NotEmpty(result.TestCases);
    }

    [Fact]
    public void Generate_SingleSelectWithAllowedValues_ProducesTestCases()
    {
        var specs = new List<FieldSpec>
        {
            new()
            {
                Name = "country",
                Type = "SingleSelect",
                AllowedValues = ["France", "Germany", "Spain"]
            },
            new() { Name = "age", Type = "Integer", Min = 18, Max = 100 }
        };

        var result = TestimizeRunner.Generate(
            specs,
            new TestimizeConfig { Enabled = true, Strategy = "HybridArtificialBeeColony" },
            fallbackDocs: null,
            suiteName: "select-test");

        Assert.NotNull(result);
        Assert.NotEmpty(result!.TestCases);
    }

    [Fact]
    public void Generate_UnmappableType_SkipsSilentlyOrReturnsNull()
    {
        var specs = new List<FieldSpec>
        {
            new() { Name = "weird", Type = "QuantumState" }
        };

        var result = TestimizeRunner.Generate(
            specs,
            new TestimizeConfig { Enabled = true },
            fallbackDocs: null,
            suiteName: "weird-test");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("HybridArtificialBeeColony", TestGenerationMode.HybridArtificialBeeColony)]
    [InlineData("hybridartificialbeecolony", TestGenerationMode.HybridArtificialBeeColony)]
    [InlineData("Pairwise", TestGenerationMode.Pairwise)]
    [InlineData("pairwise", TestGenerationMode.Pairwise)]
    [InlineData("Combinatorial", TestGenerationMode.Combinatorial)]
    [InlineData("OptimizedPairwise", TestGenerationMode.OptimizedPairwise)]
    [InlineData("OptimizedCombinatorial", TestGenerationMode.OptimizedCombinatorial)]
    [InlineData("unknown", TestGenerationMode.HybridArtificialBeeColony)]
    [InlineData(null, TestGenerationMode.HybridArtificialBeeColony)]
    public void MapStrategy_CoversAllEnumValues(string? input, TestGenerationMode expected)
    {
        Assert.Equal(expected, TestimizeRunner.MapStrategy(input));
    }

    [Theory]
    [InlineData("all", TestCaseCategory.All)]
    [InlineData("exploratory", TestCaseCategory.All)]
    [InlineData("valid", TestCaseCategory.Valid)]
    [InlineData("validation", TestCaseCategory.Validation)]
    [InlineData("invalid", TestCaseCategory.Validation)]
    [InlineData(null, TestCaseCategory.All)]
    public void MapMode_CoversAllEnumValues(string? input, TestCaseCategory expected)
    {
        Assert.Equal(expected, TestimizeRunner.MapMode(input));
    }

    [Fact]
    public void IsMappableType_RejectsUnknownType()
    {
        Assert.False(TestimizeRunner.IsMappableType(new FieldSpec { Name = "x", Type = "Widget" }));
    }

    [Fact]
    public void IsMappableType_RequiresAllowedValuesForSelectTypes()
    {
        Assert.False(TestimizeRunner.IsMappableType(new FieldSpec { Name = "x", Type = "SingleSelect" }));
        Assert.True(TestimizeRunner.IsMappableType(new FieldSpec
        {
            Name = "x",
            Type = "SingleSelect",
            AllowedValues = ["a", "b"]
        }));
    }
}
