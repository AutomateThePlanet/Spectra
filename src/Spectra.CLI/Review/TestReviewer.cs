using Spectra.Core.Models;

namespace Spectra.CLI.Review;

/// <summary>
/// Handles the interactive test review flow.
/// </summary>
public sealed class TestReviewer
{
    private readonly ReviewPresenter _presenter;
    private readonly PendingTestQueue _queue;

    public TestReviewer(PendingTestQueue queue, ReviewPresenter? presenter = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _presenter = presenter ?? new ReviewPresenter();
    }

    /// <summary>
    /// Runs the interactive review session.
    /// </summary>
    /// <returns>True if review completed, false if aborted.</returns>
    public async Task<ReviewResult> RunAsync(CancellationToken ct = default)
    {
        // Show initial summary
        _presenter.ShowSummary(_queue.GetSummary());

        if (_queue.PendingTests.Count == 0)
        {
            _presenter.ShowWarning("No tests to review.");
            return new ReviewResult { Completed = true };
        }

        var pendingTests = _queue.PendingReview.ToList();
        var currentIndex = 0;
        var reviewed = 0;
        var aborted = false;

        while (currentIndex < pendingTests.Count && !ct.IsCancellationRequested)
        {
            var pendingTest = pendingTests[currentIndex];

            // Skip already reviewed
            if (pendingTest.Decision != ReviewDecision.Pending)
            {
                currentIndex++;
                continue;
            }

            _presenter.ShowTestDetails(pendingTest);

            var action = _presenter.PromptAction(pendingTest);

            switch (action)
            {
                case ReviewAction.Accept:
                    _queue.Accept(pendingTest.Test.Id);
                    reviewed++;
                    currentIndex++;
                    break;

                case ReviewAction.Reject:
                    _queue.Reject(pendingTest.Test.Id);
                    reviewed++;
                    currentIndex++;
                    break;

                case ReviewAction.Edit:
                    var editedTest = await EditTestAsync(pendingTest.Test, ct);
                    if (editedTest is not null)
                    {
                        _queue.Replace(pendingTest.Test.Id, editedTest);
                        reviewed++;
                    }
                    currentIndex++;
                    break;

                case ReviewAction.Skip:
                    currentIndex++;
                    break;

                case ReviewAction.AcceptAllValid:
                    _queue.AcceptAllValid();
                    _presenter.ShowSuccess($"Accepted all valid tests ({_queue.AcceptedTests.Count} total)");
                    reviewed = _queue.AcceptedTests.Count + _queue.RejectedTests.Count;
                    currentIndex = pendingTests.Count; // Exit loop
                    break;

                case ReviewAction.RejectAllDuplicates:
                    _queue.RejectAllDuplicates();
                    _presenter.ShowSuccess($"Rejected all duplicates ({_queue.DuplicateTests.Count} total)");
                    // Refresh pending list to continue with remaining
                    pendingTests = _queue.PendingReview.ToList();
                    currentIndex = 0;
                    break;

                case ReviewAction.Abort:
                    if (_presenter.Confirm("Are you sure you want to abort? Pending tests will not be written."))
                    {
                        aborted = true;
                        currentIndex = pendingTests.Count; // Exit loop
                    }
                    break;
            }
        }

        // Show completion summary
        var summary = _queue.GetSummary();
        _presenter.ShowCompletion(
            summary.AcceptedCount,
            summary.RejectedCount,
            summary.PendingCount);

        return new ReviewResult
        {
            Completed = !aborted && !ct.IsCancellationRequested,
            Aborted = aborted,
            Cancelled = ct.IsCancellationRequested,
            AcceptedCount = summary.AcceptedCount,
            RejectedCount = summary.RejectedCount,
            PendingCount = summary.PendingCount
        };
    }

    /// <summary>
    /// Runs a quick auto-review (no interaction).
    /// </summary>
    public ReviewResult RunAutoReview(bool acceptValid = true, bool rejectDuplicates = true, bool rejectInvalid = true)
    {
        if (acceptValid)
        {
            _queue.AcceptAllValid();
        }

        if (rejectDuplicates)
        {
            _queue.RejectAllDuplicates();
        }

        if (rejectInvalid)
        {
            _queue.RejectAllInvalid();
        }

        var summary = _queue.GetSummary();

        return new ReviewResult
        {
            Completed = true,
            AcceptedCount = summary.AcceptedCount,
            RejectedCount = summary.RejectedCount,
            PendingCount = summary.PendingCount
        };
    }

    private Task<TestCase?> EditTestAsync(TestCase test, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var editor = new TestEditor();
        var edited = editor.Edit(test);
        return Task.FromResult(edited);
    }
}

/// <summary>
/// Result of a review session.
/// </summary>
public sealed record ReviewResult
{
    public required bool Completed { get; init; }
    public bool Aborted { get; init; }
    public bool Cancelled { get; init; }
    public int AcceptedCount { get; init; }
    public int RejectedCount { get; init; }
    public int PendingCount { get; init; }
}
