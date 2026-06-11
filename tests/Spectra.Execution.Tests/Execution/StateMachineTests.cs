using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;

namespace Spectra.MCP.Tests.Execution;

public class StateMachineTests
{
    #region Run Status Transitions

    [Theory]
    [InlineData(RunStatus.Created, RunStatus.Running, true)]
    [InlineData(RunStatus.Created, RunStatus.Paused, false)]
    [InlineData(RunStatus.Created, RunStatus.Completed, false)]
    [InlineData(RunStatus.Running, RunStatus.Paused, true)]
    [InlineData(RunStatus.Running, RunStatus.Completed, true)]
    [InlineData(RunStatus.Running, RunStatus.Cancelled, true)]
    [InlineData(RunStatus.Running, RunStatus.Created, false)]
    [InlineData(RunStatus.Paused, RunStatus.Running, true)]
    [InlineData(RunStatus.Paused, RunStatus.Cancelled, true)]
    [InlineData(RunStatus.Paused, RunStatus.Abandoned, true)]
    [InlineData(RunStatus.Paused, RunStatus.Completed, false)]
    [InlineData(RunStatus.Completed, RunStatus.Running, false)]
    [InlineData(RunStatus.Cancelled, RunStatus.Running, false)]
    [InlineData(RunStatus.Abandoned, RunStatus.Running, false)]
    public void CanTransition_RunStatus_ReturnsExpected(RunStatus current, RunStatus next, bool expected)
    {
        var result = StateMachine.CanTransition(current, next);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Transition_Run_ValidTransition_UpdatesStatus()
    {
        var run = CreateTestRun(RunStatus.Created);

        var result = StateMachine.Transition(run, RunStatus.Running);

        Assert.True(result.IsSuccess);
        Assert.Equal(RunStatus.Running, result.Value!.Status);
    }

    [Fact]
    public void Transition_Run_InvalidTransition_ReturnsFailure()
    {
        var run = CreateTestRun(RunStatus.Created);

        var result = StateMachine.Transition(run, RunStatus.Completed);

        Assert.False(result.IsSuccess);
        Assert.Contains("Cannot transition", result.ErrorMessage);
    }

    [Fact]
    public void Transition_Run_ToTerminal_SetsCompletedAt()
    {
        var run = CreateTestRun(RunStatus.Running);

        var result = StateMachine.Transition(run, RunStatus.Completed);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.CompletedAt);
    }

    [Fact]
    public void Transition_Run_ToRunning_DoesNotSetCompletedAt()
    {
        var run = CreateTestRun(RunStatus.Created);

        var result = StateMachine.Transition(run, RunStatus.Running);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.CompletedAt);
    }

    #endregion

    #region Test Status Transitions

    [Theory]
    [InlineData(TestStatus.Pending, TestStatus.InProgress, true)]
    [InlineData(TestStatus.Pending, TestStatus.Blocked, true)]
    [InlineData(TestStatus.Pending, TestStatus.Passed, false)]
    [InlineData(TestStatus.InProgress, TestStatus.Passed, true)]
    [InlineData(TestStatus.InProgress, TestStatus.Failed, true)]
    [InlineData(TestStatus.InProgress, TestStatus.Skipped, true)]
    [InlineData(TestStatus.InProgress, TestStatus.Blocked, true)]
    [InlineData(TestStatus.Passed, TestStatus.Failed, false)]
    [InlineData(TestStatus.Failed, TestStatus.Passed, false)]
    [InlineData(TestStatus.Skipped, TestStatus.InProgress, false)]
    [InlineData(TestStatus.Blocked, TestStatus.InProgress, false)]
    public void CanTransition_TestStatus_ReturnsExpected(TestStatus current, TestStatus next, bool expected)
    {
        var result = StateMachine.CanTransition(current, next);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Transition_Test_ToInProgress_SetsStartedAt()
    {
        var testResult = CreateTestResult(TestStatus.Pending);

        var result = StateMachine.Transition(testResult, TestStatus.InProgress);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.StartedAt);
    }

    [Fact]
    public void Transition_Test_ToTerminal_SetsCompletedAt()
    {
        var testResult = CreateTestResult(TestStatus.InProgress);

        var result = StateMachine.Transition(testResult, TestStatus.Passed);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.CompletedAt);
    }

    #endregion

    #region Terminal State Checks

    [Theory]
    [InlineData(RunStatus.Created, false)]
    [InlineData(RunStatus.Running, false)]
    [InlineData(RunStatus.Paused, false)]
    [InlineData(RunStatus.Completed, true)]
    [InlineData(RunStatus.Cancelled, true)]
    [InlineData(RunStatus.Abandoned, true)]
    public void IsTerminal_RunStatus_ReturnsExpected(RunStatus status, bool expected)
    {
        Assert.Equal(expected, StateMachine.IsTerminal(status));
    }

    [Theory]
    [InlineData(TestStatus.Pending, false)]
    [InlineData(TestStatus.InProgress, false)]
    [InlineData(TestStatus.Passed, true)]
    [InlineData(TestStatus.Failed, true)]
    [InlineData(TestStatus.Skipped, true)]
    [InlineData(TestStatus.Blocked, true)]
    public void IsTerminal_TestStatus_ReturnsExpected(TestStatus status, bool expected)
    {
        Assert.Equal(expected, StateMachine.IsTerminal(status));
    }

    #endregion

    #region Active State Checks

    [Theory]
    [InlineData(RunStatus.Created, false)]
    [InlineData(RunStatus.Running, true)]
    [InlineData(RunStatus.Paused, false)]
    [InlineData(RunStatus.Completed, false)]
    public void IsActive_RunStatus_ReturnsExpected(RunStatus status, bool expected)
    {
        Assert.Equal(expected, StateMachine.IsActive(status));
    }

    #endregion

    #region Valid Next States

    [Fact]
    public void GetValidNextStates_RunCreated_ReturnsRunning()
    {
        var nextStates = StateMachine.GetValidNextStates(RunStatus.Created);

        Assert.Single(nextStates);
        Assert.Contains(RunStatus.Running, nextStates);
    }

    [Fact]
    public void GetValidNextStates_RunRunning_ReturnsPausedCompletedCancelled()
    {
        var nextStates = StateMachine.GetValidNextStates(RunStatus.Running);

        Assert.Equal(3, nextStates.Count);
        Assert.Contains(RunStatus.Paused, nextStates);
        Assert.Contains(RunStatus.Completed, nextStates);
        Assert.Contains(RunStatus.Cancelled, nextStates);
    }

    [Fact]
    public void GetValidNextStates_RunCompleted_ReturnsEmpty()
    {
        var nextStates = StateMachine.GetValidNextStates(RunStatus.Completed);

        Assert.Empty(nextStates);
    }

    #endregion

    #region Helpers

    private static Run CreateTestRun(RunStatus status) => new()
    {
        RunId = Guid.NewGuid().ToString(),
        Suite = "test-suite",
        Status = status,
        StartedAt = DateTime.UtcNow,
        StartedBy = "test-user",
        UpdatedAt = DateTime.UtcNow
    };

    private static TestResult CreateTestResult(TestStatus status) => new()
    {
        RunId = Guid.NewGuid().ToString(),
        TestId = "TC-001",
        TestHandle = "test-handle",
        Status = status,
        Attempt = 1
    };

    #endregion
}
