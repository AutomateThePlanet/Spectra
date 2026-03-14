using Spectra.Core.Models;
using Spectra.Core.Validation;

namespace Spectra.CLI.Review;

/// <summary>
/// Manages generated tests pending review.
/// </summary>
public sealed class PendingTestQueue
{
    private readonly List<PendingTest> _pending = [];
    private readonly DuplicateDetector _duplicateDetector;
    private readonly double _duplicateThreshold;

    public PendingTestQueue(double duplicateThreshold = 0.6)
    {
        _duplicateThreshold = duplicateThreshold;
        _duplicateDetector = new DuplicateDetector(duplicateThreshold);
    }

    /// <summary>
    /// Gets all pending tests.
    /// </summary>
    public IReadOnlyList<PendingTest> PendingTests => _pending;

    /// <summary>
    /// Gets tests marked as valid.
    /// </summary>
    public IReadOnlyList<PendingTest> ValidTests =>
        _pending.Where(t => t.Status == PendingTestStatus.Valid).ToList();

    /// <summary>
    /// Gets tests marked as duplicates.
    /// </summary>
    public IReadOnlyList<PendingTest> DuplicateTests =>
        _pending.Where(t => t.Status == PendingTestStatus.Duplicate).ToList();

    /// <summary>
    /// Gets tests marked as invalid.
    /// </summary>
    public IReadOnlyList<PendingTest> InvalidTests =>
        _pending.Where(t => t.Status == PendingTestStatus.Invalid).ToList();

    /// <summary>
    /// Gets accepted tests.
    /// </summary>
    public IReadOnlyList<PendingTest> AcceptedTests =>
        _pending.Where(t => t.Decision == ReviewDecision.Accept).ToList();

    /// <summary>
    /// Gets rejected tests.
    /// </summary>
    public IReadOnlyList<PendingTest> RejectedTests =>
        _pending.Where(t => t.Decision == ReviewDecision.Reject).ToList();

    /// <summary>
    /// Gets tests pending review.
    /// </summary>
    public IReadOnlyList<PendingTest> PendingReview =>
        _pending.Where(t => t.Decision == ReviewDecision.Pending).ToList();

    /// <summary>
    /// Adds a test to the queue with validation against existing tests.
    /// </summary>
    public void Add(TestCase testCase, IReadOnlyList<TestCase> existingTests)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(existingTests);

        var pending = new PendingTest
        {
            Test = testCase,
            Status = PendingTestStatus.Valid,
            Decision = ReviewDecision.Pending
        };

        // Check for duplicates
        var duplicates = _duplicateDetector.FindDuplicates(testCase, existingTests);
        if (duplicates.Count > 0)
        {
            pending = pending with
            {
                Status = PendingTestStatus.Duplicate,
                DuplicateOf = duplicates[0].MatchedTestId,
                DuplicateSimilarity = duplicates[0].Similarity
            };
        }

        // Also check against other pending tests
        var pendingTestCases = _pending.Select(p => p.Test).ToList();
        var pendingDuplicates = _duplicateDetector.FindDuplicates(testCase, pendingTestCases);
        if (pendingDuplicates.Count > 0 && pending.Status == PendingTestStatus.Valid)
        {
            pending = pending with
            {
                Status = PendingTestStatus.Duplicate,
                DuplicateOf = pendingDuplicates[0].MatchedTestId,
                DuplicateSimilarity = pendingDuplicates[0].Similarity
            };
        }

        _pending.Add(pending);
    }

    /// <summary>
    /// Adds multiple tests to the queue.
    /// </summary>
    public void AddRange(IEnumerable<TestCase> testCases, IReadOnlyList<TestCase> existingTests)
    {
        ArgumentNullException.ThrowIfNull(testCases);
        ArgumentNullException.ThrowIfNull(existingTests);

        foreach (var testCase in testCases)
        {
            Add(testCase, existingTests);
        }
    }

    /// <summary>
    /// Marks a test with validation errors.
    /// </summary>
    public void MarkInvalid(string testId, IReadOnlyList<ValidationError> errors)
    {
        var pending = _pending.FirstOrDefault(p => p.Test.Id == testId);
        if (pending is null)
        {
            return;
        }

        var index = _pending.IndexOf(pending);
        _pending[index] = pending with
        {
            Status = PendingTestStatus.Invalid,
            ValidationErrors = errors
        };
    }

    /// <summary>
    /// Accepts a test for writing.
    /// </summary>
    public void Accept(string testId)
    {
        UpdateDecision(testId, ReviewDecision.Accept);
    }

    /// <summary>
    /// Rejects a test.
    /// </summary>
    public void Reject(string testId)
    {
        UpdateDecision(testId, ReviewDecision.Reject);
    }

    /// <summary>
    /// Accepts all valid tests.
    /// </summary>
    public void AcceptAllValid()
    {
        for (var i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Status == PendingTestStatus.Valid &&
                _pending[i].Decision == ReviewDecision.Pending)
            {
                _pending[i] = _pending[i] with { Decision = ReviewDecision.Accept };
            }
        }
    }

    /// <summary>
    /// Rejects all duplicates.
    /// </summary>
    public void RejectAllDuplicates()
    {
        for (var i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Status == PendingTestStatus.Duplicate &&
                _pending[i].Decision == ReviewDecision.Pending)
            {
                _pending[i] = _pending[i] with { Decision = ReviewDecision.Reject };
            }
        }
    }

    /// <summary>
    /// Rejects all invalid tests.
    /// </summary>
    public void RejectAllInvalid()
    {
        for (var i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Status == PendingTestStatus.Invalid &&
                _pending[i].Decision == ReviewDecision.Pending)
            {
                _pending[i] = _pending[i] with { Decision = ReviewDecision.Reject };
            }
        }
    }

    /// <summary>
    /// Replaces a test with an edited version.
    /// </summary>
    public void Replace(string testId, TestCase editedTest)
    {
        ArgumentNullException.ThrowIfNull(editedTest);

        var index = _pending.FindIndex(p => p.Test.Id == testId);
        if (index < 0)
        {
            return;
        }

        _pending[index] = _pending[index] with
        {
            Test = editedTest,
            Status = PendingTestStatus.Valid, // Re-validate if needed
            Decision = ReviewDecision.Accept
        };
    }

    /// <summary>
    /// Gets tests to write (accepted valid tests).
    /// </summary>
    public IReadOnlyList<TestCase> GetTestsToWrite()
    {
        return _pending
            .Where(p => p.Decision == ReviewDecision.Accept)
            .Select(p => p.Test)
            .ToList();
    }

    /// <summary>
    /// Gets summary statistics.
    /// </summary>
    public QueueSummary GetSummary()
    {
        return new QueueSummary
        {
            TotalCount = _pending.Count,
            ValidCount = ValidTests.Count,
            DuplicateCount = DuplicateTests.Count,
            InvalidCount = InvalidTests.Count,
            AcceptedCount = AcceptedTests.Count,
            RejectedCount = RejectedTests.Count,
            PendingCount = PendingReview.Count
        };
    }

    /// <summary>
    /// Clears all pending tests.
    /// </summary>
    public void Clear()
    {
        _pending.Clear();
    }

    private void UpdateDecision(string testId, ReviewDecision decision)
    {
        var index = _pending.FindIndex(p => p.Test.Id == testId);
        if (index >= 0)
        {
            _pending[index] = _pending[index] with { Decision = decision };
        }
    }
}

/// <summary>
/// A test pending review.
/// </summary>
public sealed record PendingTest
{
    public required TestCase Test { get; init; }
    public required PendingTestStatus Status { get; init; }
    public required ReviewDecision Decision { get; init; }
    public string? DuplicateOf { get; init; }
    public double? DuplicateSimilarity { get; init; }
    public IReadOnlyList<ValidationError>? ValidationErrors { get; init; }
}

/// <summary>
/// Status of a pending test.
/// </summary>
public enum PendingTestStatus
{
    Valid,
    Duplicate,
    Invalid
}

/// <summary>
/// Review decision for a test.
/// </summary>
public enum ReviewDecision
{
    Pending,
    Accept,
    Reject
}

/// <summary>
/// Summary of queue statistics.
/// </summary>
public sealed record QueueSummary
{
    public required int TotalCount { get; init; }
    public required int ValidCount { get; init; }
    public required int DuplicateCount { get; init; }
    public required int InvalidCount { get; init; }
    public required int AcceptedCount { get; init; }
    public required int RejectedCount { get; init; }
    public required int PendingCount { get; init; }
}
