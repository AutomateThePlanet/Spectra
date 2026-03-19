using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;

namespace Spectra.MCP.Tests.Execution;

public class TestQueueReconstructionTests
{
    [Fact]
    public void AddFromResult_AddsTestToQueue()
    {
        var queue = new TestQueue("run-1");
        var result = new TestResult
        {
            RunId = "run-1",
            TestId = "TC-001",
            TestHandle = "run-1-TC-001-abc",
            Status = TestStatus.Pending,
            Attempt = 1
        };

        queue.AddFromResult(result);

        Assert.Single(queue.Tests);
        Assert.Equal("TC-001", queue.Tests[0].TestId);
        Assert.Equal("run-1-TC-001-abc", queue.Tests[0].TestHandle);
        Assert.Equal(TestStatus.Pending, queue.Tests[0].Status);
    }

    [Fact]
    public void AddFromResult_InProgressTest_SetsCurrentIndex()
    {
        var queue = new TestQueue("run-1");

        // Add 3 tests, second one is in progress
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-001", TestHandle = "h1", Status = TestStatus.Passed, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-002", TestHandle = "h2", Status = TestStatus.InProgress, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-003", TestHandle = "h3", Status = TestStatus.Pending, Attempt = 1 });

        Assert.Equal(3, queue.TotalCount);
        Assert.Equal(1, queue.CurrentIndex); // Should point to TC-002 (in progress)
    }

    [Fact]
    public void GetNext_ReturnsInProgressTestFirst()
    {
        var queue = new TestQueue("run-1");
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-001", TestHandle = "h1", Status = TestStatus.Passed, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-002", TestHandle = "h2", Status = TestStatus.InProgress, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-003", TestHandle = "h3", Status = TestStatus.Pending, Attempt = 1 });

        var next = queue.GetNext();

        Assert.NotNull(next);
        Assert.Equal("TC-002", next.TestId);
        Assert.Equal(TestStatus.InProgress, next.Status);
    }

    [Fact]
    public void GetNext_NoInProgress_ReturnsPendingTest()
    {
        var queue = new TestQueue("run-1");
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-001", TestHandle = "h1", Status = TestStatus.Passed, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-002", TestHandle = "h2", Status = TestStatus.Failed, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-003", TestHandle = "h3", Status = TestStatus.Pending, Attempt = 1 });

        var next = queue.GetNext();

        Assert.NotNull(next);
        Assert.Equal("TC-003", next.TestId);
        Assert.Equal(TestStatus.Pending, next.Status);
    }

    [Fact]
    public void GetNext_AllCompleted_ReturnsNull()
    {
        var queue = new TestQueue("run-1");
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-001", TestHandle = "h1", Status = TestStatus.Passed, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-002", TestHandle = "h2", Status = TestStatus.Failed, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-003", TestHandle = "h3", Status = TestStatus.Skipped, Attempt = 1 });

        var next = queue.GetNext();

        Assert.Null(next);
    }

    [Fact]
    public void GetNext_ReturnsInProgressBeforePending()
    {
        // Scenario: Test was started, client crashed, reconnected
        // InProgress test should be returned first even if there are earlier pending tests
        var queue = new TestQueue("run-1");
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-001", TestHandle = "h1", Status = TestStatus.Pending, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-002", TestHandle = "h2", Status = TestStatus.InProgress, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-003", TestHandle = "h3", Status = TestStatus.Pending, Attempt = 1 });

        var next = queue.GetNext();

        // Should return TC-002 (InProgress) not TC-001 (Pending)
        Assert.NotNull(next);
        Assert.Equal("TC-002", next.TestId);
    }

    [Fact]
    public void ReconstructedQueue_CanMarkCompleted()
    {
        var queue = new TestQueue("run-1");
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-001", TestHandle = "h1", Status = TestStatus.InProgress, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-002", TestHandle = "h2", Status = TestStatus.Pending, Attempt = 1 });

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
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-001", TestHandle = "h1", Status = TestStatus.Passed, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-002", TestHandle = "h2", Status = TestStatus.Failed, Attempt = 1 });
        queue.AddFromResult(new TestResult { RunId = "run-1", TestId = "TC-003", TestHandle = "h3", Status = TestStatus.Pending, Attempt = 1 });

        Assert.Equal(2, queue.CompletedCount);
        Assert.Equal(1, queue.PendingCount);
        Assert.Equal("2/3", queue.GetProgress());
    }
}
