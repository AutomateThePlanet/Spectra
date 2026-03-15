namespace Spectra.Core.Models.Profile;

/// <summary>
/// Tracks progress through the interactive questionnaire.
/// </summary>
public sealed class QuestionnaireState
{
    /// <summary>
    /// Gets the current question index (0-based).
    /// </summary>
    public int CurrentStep { get; private set; }

    /// <summary>
    /// Gets the total number of questions.
    /// </summary>
    public int TotalSteps { get; }

    /// <summary>
    /// Gets the collected answers by question key.
    /// </summary>
    public Dictionary<string, object> Answers { get; } = new();

    /// <summary>
    /// Gets whether the questionnaire is complete.
    /// </summary>
    public bool Completed => CurrentStep >= TotalSteps;

    /// <summary>
    /// Gets the progress percentage.
    /// </summary>
    public int ProgressPercent => TotalSteps > 0 ? (CurrentStep * 100) / TotalSteps : 0;

    /// <summary>
    /// Creates a new questionnaire state.
    /// </summary>
    public QuestionnaireState(int totalSteps)
    {
        TotalSteps = totalSteps;
        CurrentStep = 0;
    }

    /// <summary>
    /// Records an answer and advances to the next question.
    /// </summary>
    public void RecordAnswer(string key, object value)
    {
        Answers[key] = value;
        CurrentStep++;
    }

    /// <summary>
    /// Skips the current question using its default value.
    /// </summary>
    public void Skip(string key, object? defaultValue)
    {
        if (defaultValue is not null)
        {
            Answers[key] = defaultValue;
        }
        CurrentStep++;
    }

    /// <summary>
    /// Goes back to the previous question.
    /// </summary>
    public void GoBack()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
        }
    }

    /// <summary>
    /// Resets the questionnaire state.
    /// </summary>
    public void Reset()
    {
        CurrentStep = 0;
        Answers.Clear();
    }

    /// <summary>
    /// Gets an answer value or default.
    /// </summary>
    public T GetAnswer<T>(string key, T defaultValue)
    {
        return Answers.TryGetValue(key, out var value) && value is T typedValue
            ? typedValue
            : defaultValue;
    }

    /// <summary>
    /// Tries to get an answer value.
    /// </summary>
    public bool TryGetAnswer<T>(string key, out T value)
    {
        if (Answers.TryGetValue(key, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default!;
        return false;
    }
}
