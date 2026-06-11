using Spectra.Core.Models.Execution;

namespace Spectra.MCP.Tests.Models;

public class TestHandleTests
{
    [Fact]
    public void Generate_ValidInputs_ReturnsFormattedHandle()
    {
        var runId = "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c";
        var testId = "TC-101";

        var handle = TestHandle.Generate(runId, testId);

        Assert.NotNull(handle);
        Assert.StartsWith("a3f7c291-", handle);
        Assert.Contains("TC-101", handle);
        Assert.Equal(3, handle.Split('-').Length - 1); // 3 hyphens (prefix-TC-101-suffix)
    }

    [Fact]
    public void Generate_MultipleCalls_ReturnsDifferentSuffixes()
    {
        var runId = "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c";
        var testId = "TC-101";

        var handle1 = TestHandle.Generate(runId, testId);
        var handle2 = TestHandle.Generate(runId, testId);

        Assert.NotEqual(handle1, handle2);
    }

    [Fact]
    public void Generate_ShortRunId_UsesFull()
    {
        var runId = "short";
        var testId = "TC-001";

        var handle = TestHandle.Generate(runId, testId);

        Assert.StartsWith("short-", handle);
    }

    [Theory]
    [InlineData(null, "TC-001")]
    [InlineData("", "TC-001")]
    [InlineData("run-id", null)]
    [InlineData("run-id", "")]
    public void Generate_InvalidInputs_ThrowsArgumentException(string? runId, string? testId)
    {
        // ArgumentNullException and ArgumentException are both acceptable
        Assert.ThrowsAny<ArgumentException>(() => TestHandle.Generate(runId!, testId!));
    }

    [Fact]
    public void Validate_MatchingHandle_ReturnsTrue()
    {
        var runId = "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c";
        var testId = "TC-101";
        var handle = TestHandle.Generate(runId, testId);

        var isValid = TestHandle.Validate(handle, runId, testId);

        Assert.True(isValid);
    }

    [Fact]
    public void Validate_WrongRunId_ReturnsFalse()
    {
        var runId = "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c";
        var testId = "TC-101";
        var handle = TestHandle.Generate(runId, testId);

        var isValid = TestHandle.Validate(handle, "different-run-id", testId);

        Assert.False(isValid);
    }

    [Fact]
    public void Validate_WrongTestId_ReturnsFalse()
    {
        var runId = "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c";
        var testId = "TC-101";
        var handle = TestHandle.Generate(runId, testId);

        var isValid = TestHandle.Validate(handle, runId, "TC-999");

        Assert.False(isValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("no-hyphens")]
    public void Validate_InvalidHandle_ReturnsFalse(string? handle)
    {
        var isValid = TestHandle.Validate(handle!, "run-id", "test-id");

        Assert.False(isValid);
    }

    [Fact]
    public void ExtractTestId_ValidHandle_ReturnsTestId()
    {
        var runId = "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c";
        var testId = "TC-101";
        var handle = TestHandle.Generate(runId, testId);

        var extractedTestId = TestHandle.ExtractTestId(handle);

        Assert.Equal(testId, extractedTestId);
    }

    [Fact]
    public void ExtractTestId_TestIdWithHyphen_ReturnsFullTestId()
    {
        var runId = "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c";
        var testId = "TC-LONG-101";
        var handle = TestHandle.Generate(runId, testId);

        var extractedTestId = TestHandle.ExtractTestId(handle);

        Assert.Equal(testId, extractedTestId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public void ExtractTestId_InvalidHandle_ReturnsNull(string? handle)
    {
        var result = TestHandle.ExtractTestId(handle!);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractRunPrefix_ValidHandle_ReturnsPrefix()
    {
        var runId = "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c";
        var testId = "TC-101";
        var handle = TestHandle.Generate(runId, testId);

        var prefix = TestHandle.ExtractRunPrefix(handle);

        Assert.Equal("a3f7c291", prefix);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nohyphens")]
    public void ExtractRunPrefix_InvalidHandle_ReturnsNull(string? handle)
    {
        var result = TestHandle.ExtractRunPrefix(handle!);

        Assert.Null(result);
    }
}
