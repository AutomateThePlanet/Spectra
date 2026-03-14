using System.Security.Cryptography;

namespace Spectra.Core.Models.Execution;

/// <summary>
/// Generates and validates opaque test handles to prevent context manipulation.
/// Format: {run_uuid_prefix}-{test_id}-{random_suffix}
/// </summary>
public static class TestHandle
{
    private const int PrefixLength = 8;
    private const int SuffixLength = 4;

    /// <summary>
    /// Generates a new test handle for the given run and test.
    /// </summary>
    /// <param name="runId">The run UUID.</param>
    /// <param name="testId">The test case ID (e.g., TC-101).</param>
    /// <returns>An opaque handle string.</returns>
    public static string Generate(string runId, string testId)
    {
        ArgumentException.ThrowIfNullOrEmpty(runId);
        ArgumentException.ThrowIfNullOrEmpty(testId);

        var prefix = runId.Length >= PrefixLength
            ? runId[..PrefixLength]
            : runId;

        var randomBytes = RandomNumberGenerator.GetBytes(3);
        var random = Convert.ToBase64String(randomBytes)
            .Replace("+", "x")
            .Replace("/", "k")
            .Replace("=", "")
            [..SuffixLength];

        return $"{prefix}-{testId}-{random}";
    }

    /// <summary>
    /// Validates that a test handle matches the expected run and test.
    /// </summary>
    /// <param name="handle">The handle to validate.</param>
    /// <param name="runId">The expected run UUID.</param>
    /// <param name="testId">The expected test case ID.</param>
    /// <returns>True if the handle matches both run and test.</returns>
    public static bool Validate(string handle, string runId, string testId)
    {
        if (string.IsNullOrEmpty(handle) ||
            string.IsNullOrEmpty(runId) ||
            string.IsNullOrEmpty(testId))
        {
            return false;
        }

        var parts = handle.Split('-');
        if (parts.Length < 3)
        {
            return false;
        }

        // First part should match run prefix
        var expectedPrefix = runId.Length >= PrefixLength
            ? runId[..PrefixLength]
            : runId;

        if (parts[0] != expectedPrefix)
        {
            return false;
        }

        // Middle part(s) should match test ID (test ID may contain hyphens)
        var handleTestId = string.Join("-", parts[1..^1]);
        return handleTestId == testId;
    }

    /// <summary>
    /// Extracts the test ID from a handle.
    /// </summary>
    /// <param name="handle">The test handle.</param>
    /// <returns>The test ID, or null if the handle is invalid.</returns>
    public static string? ExtractTestId(string handle)
    {
        if (string.IsNullOrEmpty(handle))
        {
            return null;
        }

        var parts = handle.Split('-');
        if (parts.Length < 3)
        {
            return null;
        }

        // Middle part(s) form the test ID
        return string.Join("-", parts[1..^1]);
    }

    /// <summary>
    /// Extracts the run prefix from a handle.
    /// </summary>
    /// <param name="handle">The test handle.</param>
    /// <returns>The run prefix, or null if the handle is invalid.</returns>
    public static string? ExtractRunPrefix(string handle)
    {
        if (string.IsNullOrEmpty(handle))
        {
            return null;
        }

        var dashIndex = handle.IndexOf('-');
        return dashIndex > 0 ? handle[..dashIndex] : null;
    }
}
