using Spectra.Core.Baseline;

namespace Spectra.Core.Tests.Baseline;

/// <summary>Spec 084 (FR-004, FR-006): fingerprint format and what does or doesn't change it.</summary>
public class SourceFingerprintTests
{
    [Fact]
    public void Section_IsSha256OfTheText_InThePrefixedFormat() =>
        // sha256("a\n")
        Assert.Equal("sha256:87428fc522803d31065e7bce3cf03fe475096631e5e07bbd7a0fde60c4cf25c7", SourceFingerprint.OfSection("a\n"));

    [Theory]
    [InlineData("x\r\ny\r\n")]
    [InlineData("x\ry\r")]
    [InlineData("﻿x\ny\n")]
    public void LineEndingsAndBom_DoNotChangeTheFingerprint(string variant) =>
        Assert.Equal(SourceFingerprint.OfSection("x\ny\n"), SourceFingerprint.OfSection(variant));

    [Theory]
    [InlineData("x \ny\n")]   // trailing space
    [InlineData("X\ny\n")]    // case
    [InlineData("**x**\ny\n")] // markup
    public void RawChanges_ChangeTheFingerprint(string variant) =>
        Assert.NotEqual(SourceFingerprint.OfSection("x\ny\n"), SourceFingerprint.OfSection(variant));

    [Fact]
    public void Criterion_IsOverTheNormalisedText() =>
        Assert.Equal(SourceFingerprint.OfCriterion("Users **MUST** log in"), SourceFingerprint.OfCriterion("users must  log in"));

    [Theory]
    [InlineData("sha256:87428fc522803d31065e7bce3cf03fe475096631e5e07bbd7a0fde60c4cf25c7", true)]
    [InlineData("sha256:87428FC522803D31065E7BCE3CF03FE475096631E5E07BBD7A0FDE60C4CF25C7", false)]
    [InlineData("87428fc522803d31065e7bce3cf03fe475096631e5e07bbd7a0fde60c4cf25c7", false)]
    [InlineData("sha256:87428fc5", false)]
    [InlineData("sha1:87428fc522803d31065e7bce3cf03fe475096631e5e07bbd7a0fde60c4cf25c7", false)]
    [InlineData(null, false)]
    public void IsValid(string? fingerprint, bool valid) => Assert.Equal(valid, SourceFingerprint.IsValid(fingerprint));

    [Fact]
    public void Hex_StripsThePrefix() =>
        Assert.Equal("abc", SourceFingerprint.Hex("sha256:abc"));
}
