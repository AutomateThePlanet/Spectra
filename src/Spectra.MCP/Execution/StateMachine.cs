using Spectra.Core.Models.Execution;

namespace Spectra.MCP.Execution;

/// <summary>
/// Validates and enforces state transitions for runs and tests.
/// Implements deterministic state machine per spec.
/// </summary>
public static class StateMachine
{
    /// <summary>
    /// Valid state transitions for RunStatus.
    /// </summary>
    private static readonly Dictionary<RunStatus, HashSet<RunStatus>> ValidRunTransitions = new()
    {
        [RunStatus.Created] = [RunStatus.Running],
        [RunStatus.Running] = [RunStatus.Paused, RunStatus.Completed, RunStatus.Cancelled],
        [RunStatus.Paused] = [RunStatus.Running, RunStatus.Cancelled, RunStatus.Abandoned],
        [RunStatus.Completed] = [],
        [RunStatus.Cancelled] = [],
        [RunStatus.Abandoned] = []
    };

    /// <summary>
    /// Valid state transitions for TestStatus.
    /// </summary>
    private static readonly Dictionary<TestStatus, HashSet<TestStatus>> ValidTestTransitions = new()
    {
        [TestStatus.Pending] = [TestStatus.InProgress, TestStatus.Blocked],
        [TestStatus.InProgress] = [TestStatus.Passed, TestStatus.Failed, TestStatus.Skipped],
        [TestStatus.Passed] = [],
        [TestStatus.Failed] = [],
        [TestStatus.Skipped] = [],
        [TestStatus.Blocked] = []
    };

    /// <summary>
    /// Checks if a run status transition is valid.
    /// </summary>
    /// <param name="current">Current run status.</param>
    /// <param name="next">Desired next status.</param>
    /// <returns>True if transition is allowed.</returns>
    public static bool CanTransition(RunStatus current, RunStatus next)
    {
        return ValidRunTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next);
    }

    /// <summary>
    /// Checks if a test status transition is valid.
    /// </summary>
    /// <param name="current">Current test status.</param>
    /// <param name="next">Desired next status.</param>
    /// <returns>True if transition is allowed.</returns>
    public static bool CanTransition(TestStatus current, TestStatus next)
    {
        return ValidTestTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next);
    }

    /// <summary>
    /// Validates and performs a run status transition.
    /// </summary>
    /// <param name="run">The run to transition.</param>
    /// <param name="next">Desired next status.</param>
    /// <returns>Success result with updated run, or failure with error.</returns>
    public static TransitionResult<Run> Transition(Run run, RunStatus next)
    {
        if (!CanTransition(run.Status, next))
        {
            return TransitionResult<Run>.Invalid(
                run.Status,
                next,
                $"Cannot transition run from {run.Status} to {next}");
        }

        run.Status = next;
        run.UpdatedAt = DateTime.UtcNow;

        if (next is RunStatus.Completed or RunStatus.Cancelled or RunStatus.Abandoned)
        {
            run.CompletedAt = DateTime.UtcNow;
        }

        return TransitionResult<Run>.Success(run);
    }

    /// <summary>
    /// Validates and performs a test status transition.
    /// </summary>
    /// <param name="result">The test result to transition.</param>
    /// <param name="next">Desired next status.</param>
    /// <returns>Success result with updated test, or failure with error.</returns>
    public static TransitionResult<TestResult> Transition(TestResult result, TestStatus next)
    {
        if (!CanTransition(result.Status, next))
        {
            return TransitionResult<TestResult>.Invalid(
                result.Status,
                next,
                $"Cannot transition test from {result.Status} to {next}");
        }

        result.Status = next;

        if (next == TestStatus.InProgress)
        {
            result.StartedAt = DateTime.UtcNow;
        }
        else if (next is TestStatus.Passed or TestStatus.Failed or TestStatus.Skipped or TestStatus.Blocked)
        {
            result.CompletedAt = DateTime.UtcNow;
        }

        return TransitionResult<TestResult>.Success(result);
    }

    /// <summary>
    /// Gets valid next states for a run.
    /// </summary>
    public static IReadOnlySet<RunStatus> GetValidNextStates(RunStatus current)
    {
        return ValidRunTransitions.TryGetValue(current, out var allowed)
            ? allowed
            : new HashSet<RunStatus>();
    }

    /// <summary>
    /// Gets valid next states for a test.
    /// </summary>
    public static IReadOnlySet<TestStatus> GetValidNextStates(TestStatus current)
    {
        return ValidTestTransitions.TryGetValue(current, out var allowed)
            ? allowed
            : new HashSet<TestStatus>();
    }

    /// <summary>
    /// Checks if a run is in a terminal state.
    /// </summary>
    public static bool IsTerminal(RunStatus status)
    {
        return status is RunStatus.Completed or RunStatus.Cancelled or RunStatus.Abandoned;
    }

    /// <summary>
    /// Checks if a test is in a terminal state.
    /// </summary>
    public static bool IsTerminal(TestStatus status)
    {
        return status is TestStatus.Passed or TestStatus.Failed or TestStatus.Skipped or TestStatus.Blocked;
    }

    /// <summary>
    /// Checks if a run is active (can process tests).
    /// </summary>
    public static bool IsActive(RunStatus status)
    {
        return status == RunStatus.Running;
    }
}

/// <summary>
/// Result of a state transition attempt.
/// </summary>
public sealed class TransitionResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Value { get; private init; }
    public string? FromState { get; private init; }
    public string? ToState { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static TransitionResult<T> Success(T value) => new()
    {
        IsSuccess = true,
        Value = value
    };

    public static TransitionResult<T> Invalid<TStatus>(TStatus from, TStatus to, string message) where TStatus : Enum => new()
    {
        IsSuccess = false,
        FromState = from.ToString(),
        ToState = to.ToString(),
        ErrorMessage = message
    };
}
