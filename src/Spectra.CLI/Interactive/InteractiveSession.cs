using Spectra.Core.Models;

namespace Spectra.CLI.Interactive;

/// <summary>
/// State machine for interactive generation/update session.
/// </summary>
public sealed class InteractiveSession
{
    /// <summary>
    /// Unique session identifier.
    /// </summary>
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Session mode (Generate or Update).
    /// </summary>
    public SessionMode Mode { get; init; }

    /// <summary>
    /// Current state in the flow.
    /// </summary>
    public SessionState State { get; private set; } = SessionState.SuiteSelection;

    /// <summary>
    /// Selected suite name.
    /// </summary>
    public string? Suite { get; private set; }

    /// <summary>
    /// Suite directory path.
    /// </summary>
    public string? SuitePath { get; private set; }

    /// <summary>
    /// User-provided focus description.
    /// </summary>
    public string? Focus { get; private set; }

    /// <summary>
    /// Type of tests to generate.
    /// </summary>
    public TestTypeSelection? TestType { get; private set; }

    /// <summary>
    /// Selected gaps for generation.
    /// </summary>
    public List<CoverageGap> SelectedGaps { get; } = [];

    /// <summary>
    /// Tests generated in this session.
    /// </summary>
    public List<TestCase> GeneratedTests { get; } = [];

    /// <summary>
    /// Remaining coverage gaps.
    /// </summary>
    public List<CoverageGap> RemainingGaps { get; } = [];

    /// <summary>
    /// Session start time.
    /// </summary>
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;

    /// <summary>
    /// Sets the selected suite and advances state.
    /// </summary>
    public void SetSuite(string name, string path)
    {
        Suite = name;
        SuitePath = path;
        State = SessionState.TestTypeSelection;
    }

    /// <summary>
    /// Sets the test type and advances state.
    /// </summary>
    public void SetTestType(TestTypeSelection type)
    {
        TestType = type;

        // If type requires focus input, go to FocusInput
        if (type is TestTypeSelection.SpecificArea or TestTypeSelection.FreeDescription)
        {
            State = SessionState.FocusInput;
        }
        else
        {
            State = SessionState.GapAnalysis;
        }
    }

    /// <summary>
    /// Sets the focus and advances state.
    /// </summary>
    public void SetFocus(string? focus)
    {
        Focus = focus;
        State = SessionState.GapAnalysis;
    }

    /// <summary>
    /// Starts generation phase.
    /// </summary>
    public void StartGenerating()
    {
        State = SessionState.Generating;
    }

    /// <summary>
    /// Records generated tests and advances to results.
    /// </summary>
    public void RecordGeneration(IEnumerable<TestCase> tests, IEnumerable<CoverageGap> remainingGaps)
    {
        GeneratedTests.AddRange(tests);
        RemainingGaps.Clear();
        RemainingGaps.AddRange(remainingGaps);
        State = SessionState.Results;
    }

    /// <summary>
    /// Moves to gap selection for follow-up generation.
    /// </summary>
    public void MoveToGapSelection()
    {
        if (RemainingGaps.Count > 0)
        {
            State = SessionState.GapSelection;
        }
        else
        {
            Complete();
        }
    }

    /// <summary>
    /// Sets selected gaps for next generation round.
    /// </summary>
    public void SelectGaps(IEnumerable<CoverageGap> gaps)
    {
        SelectedGaps.Clear();
        SelectedGaps.AddRange(gaps);
        State = SessionState.Generating;
    }

    /// <summary>
    /// Marks session as complete.
    /// </summary>
    public void Complete()
    {
        State = SessionState.Complete;
    }

    /// <summary>
    /// Checks if session is complete.
    /// </summary>
    public bool IsComplete => State == SessionState.Complete;
}
