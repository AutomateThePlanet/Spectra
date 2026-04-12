using Spectra.CLI.Agent.Testimize;

namespace Spectra.CLI.Tests.Agent.Testimize;

/// <summary>
/// Spec 038 / v1.48.3: local regex-based field-spec extractor. Tests the
/// shared <see cref="Spectra.Core.Models.Testimize.FieldSpec"/> shape that
/// both the AI-callable tool and the TestimizeRunner regex fallback produce.
/// </summary>
public class AnalyzeFieldSpecTests
{
    [Fact]
    public void Extract_TextRange_ProducesTextFieldWithMinMaxLength()
    {
        var fields = FieldSpecAnalysisTools.ExtractFields("The username must be 3 to 20 characters.");

        Assert.Single(fields);
        Assert.Equal("text", fields[0].Type);
        Assert.Equal(3, fields[0].MinLength);
        Assert.Equal(20, fields[0].MaxLength);
    }

    [Fact]
    public void Extract_HyphenRange_ProducesTextFieldWithMinMaxLength()
    {
        var fields = FieldSpecAnalysisTools.ExtractFields("Accepts 5-50 characters.");

        Assert.Single(fields);
        Assert.Equal("text", fields[0].Type);
        Assert.Equal(5, fields[0].MinLength);
        Assert.Equal(50, fields[0].MaxLength);
    }

    [Fact]
    public void Extract_BetweenRange_ProducesIntegerField()
    {
        var fields = FieldSpecAnalysisTools.ExtractFields("Age must be between 18 and 100.");

        Assert.Single(fields);
        Assert.Equal("integer", fields[0].Type);
        Assert.Equal(18, fields[0].Min);
        Assert.Equal(100, fields[0].Max);
    }

    [Fact]
    public void Extract_EmailMention_ProducesEmailField()
    {
        var fields = FieldSpecAnalysisTools.ExtractFields("User must enter a valid email address.");

        Assert.Contains(fields, f => f.Type == "email");
    }

    [Fact]
    public void Extract_NoFields_ReturnsEmpty()
    {
        var fields = FieldSpecAnalysisTools.ExtractFields("Click the button to save.");
        Assert.Empty(fields);
    }

    [Fact]
    public void Extract_RequiredField_SetsRequiredFlagAndMessage()
    {
        var fields = FieldSpecAnalysisTools.ExtractFields("This is a required field.");

        Assert.Single(fields);
        Assert.True(fields[0].Required);
        Assert.Equal("This field is required", fields[0].ExpectedInvalidMessage);
    }

    [Fact]
    public void Extract_EmptyText_ReturnsEmpty()
    {
        Assert.Empty(FieldSpecAnalysisTools.ExtractFields(""));
        Assert.Empty(FieldSpecAnalysisTools.ExtractFields(null!));
    }
}
