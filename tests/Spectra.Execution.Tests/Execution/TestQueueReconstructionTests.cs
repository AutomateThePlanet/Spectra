using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;

namespace Spectra.MCP.Tests.Execution;

/// <summary>
/// Exercises the lossless reconstruction primitive <see cref="TestQueue.AddReconstructed"/>
/// (spec 064). Migrated from the former lossy <c>AddFromResult</c> path: the same queue-mechanics
/// assertions are preserved, and orchestration fields (title/priority/depends_on) are now asserted
/// to be restored rather than silently defaulted.
/// </summary>
public class TestQueueReconstructionTests
{
    private static QueueSnapshotEntry Snapshot(
        string testId,
        int orderIndex,
        string title = "Title",
        string priority = "medium",
        string? dependsOn = null) => new()
    {
        RunId = "run-1",
        TestId = testId,
        Title = title,
        Priority = priority,
        DependsOn = dependsOn,
        OrderIndex = orderIndex
    };

    [Fact]
    public void AddReconstructed_AddsTestToQueue_WithRestoredOrchestration()
    {
        var queue = new TestQueue("run-1");

        queue.AddReconstructed(
            Snapshot("TC-001", 0, title: "Login works", priority: "high", dependsOn: "TC-000"),
            TestStatus.Pending,
            "run-1-TC-001-abc");

        Assert.Single(queue.Tests);
        Assert.Equal("TC-001", queue.Tests[0].TestId);
        Assert.Equal("run-1-TC-001-abc", queue.Tests[0].TestHandle);
        Assert.Equal(TestStatus.Pending, queue.Tests[0].Status);
        // Orchestration is restored from the snapshot (no longer defaulted).
        Assert.Equal("Login works", queue.Tests[0].Title);
        Assert.Equal(Priority.High, queue.Tests[0].Priority);
        Assert.Equal("TC-000", queue.Tests[0].DependsOn);
    }

    [Fact]
    public void AddReconstructed_InProgressTest_SetsCurrentIndex()
    {
        var queue = new TestQueue("run-1");

        // Add 3 tests, second one is in progress
        queue.AddReconstructed(Snapshot("TC-001", 0), TestStatus.Passed, "h1");
        queue.AddReconstructed(Snapshot("TC-002", 1), TestStatus.InProgress, "h2");
        queue.AddReconstructed(Snapshot("TC-003", 2), TestStatus.Pending, "h3");

        Assert.Equal(3, queue.TotalCount);
        Assert.Equal(1, queue.CurrentIndex); // Should point to TC-002 (in progress)
    }

    [Fact]
    public void GetNext_ReturnsInProgressTestFirst()
    {
        var queue = new TestQueue("run-1");
        queue.AddReconstructed(Snapshot("TC-001", 0), TestStatus.Passed, "h1");
        queue.AddReconstructed(Snapshot("TC-002", 1), TestStatus.InProgress, "h2");
        queue.AddReconstructed(Snapshot("TC-003", 2), TestStatus.Pending, "h3");

        var next = queue.GetNext();

        Assert.NotNull(next);
        Assert.Equal("TC-002", next.TestId);
        Assert.Equal(TestStatus.InProgress, next.Status);
    }

    [Fact]
    public void GetNext_NoInProgress_ReturnsPendingTest()
    {
        var queue = new TestQueue("run-1");
        queue.AddReconstructed(Snapshot("TC-001", 0), TestStatus.Passed, "h1");
        queue.AddReconstructed(Snapshot("TC-002", 1), TestStatus.Failed, "h2");
        queue.AddReconstructed(Snapshot("TC-003", 2), TestStatus.Pending, "h3");

        var next = queue.GetNext();

        Assert.NotNull(next);
        Assert.Equal("TC-003", next.TestId);
        Assert.Equal(TestStatus.Pending, next.Status);
    }

    [Fact]
    public void GetNext_AllCompleted_ReturnsNull()
    {
        var queue = new TestQueue("run-1");
        queue.AddReconstructed(Snapshot("TC-001", 0), TestStatus.Passed, "h1");
        queue.AddReconstructed(Snapshot("TC-002", 1), TestStatus.Failed, "h2");
        queue.AddReconstructed(Snapshot("TC-003", 2), TestStatus.Skipped, "h3");

        var next = queue.GetNext();

        Assert.Null(next);
    }

    [Fact]
    public void GetNext_ReturnsInProgressBeforePending()
    {
        // Scenario: Test was started, client crashed, reconnected
        // InProgress test should be returned first even if there are earlier pending tests
        var queue = new TestQueue("run-1");
        queue.AddReconstructed(Snapshot("TC-001", 0), TestStatus.Pending, "h1");
        queue.AddReconstructed(Snapshot("TC-002", 1), TestStatus.InProgress, "h2");
        queue.AddReconstructed(Snapshot("TC-003", 2), TestStatus.Pending, "h3");

        var next = queue.GetNext();

        // Should return TC-002 (InProgress) not TC-001 (Pending)
        Assert.NotNull(next);
        Assert.Equal("TC-002", next.TestId);
    }

    [Fact]
    public void ReconstructedQueue_CanMarkCompleted()
    {
        var queue = new TestQueue("run-1");
        queue.AddReconstructed(Snapshot("TC-001", 0), TestStatus.InProgress, "h1");
        queue.AddReconstructed(Snapshot("TC-002", 1), TestStatus.Pending, "h2");

        // Complete the in-progress test
        queue.MarkCompleted("h1", TestStatus.Passed);

        // Now GetNext should return the pending test
        var next = queue.GetNext();
        Assert.NotNull(next);
        Assert.Equal("TC-002", next.TestId);
    }

    [Fact]
    public void ReconstructedQueue_PreservesCompletedCount()
    {
        var queue = new TestQueue("run-1");
        queue.AddReconstructed(Snapshot("TC-001", 0), TestStatus.Passed, "h1");
        queue.AddReconstructed(Snapshot("TC-002", 1), TestStatus.Failed, "h2");
        queue.AddReconstructed(Snapshot("TC-003", 2), TestStatus.Pending, "h3");

        Assert.Equal(2, queue.CompletedCount);
        Assert.Equal(1, queue.PendingCount);
        Assert.Equal("2/3", queue.GetProgress());
    }
}
