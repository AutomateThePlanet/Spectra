namespace Spectra.Core.Models.Profile;

/// <summary>
/// A single questionnaire question.
/// </summary>
public sealed class Question
{
    /// <summary>
    /// Gets or sets the unique question identifier.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the question text to display.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of response expected.
    /// </summary>
    public QuestionType Type { get; init; }

    /// <summary>
    /// Gets or sets the valid options for choice questions.
    /// </summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>
    /// Gets or sets the default value if user skips.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Gets or sets additional context for the question.
    /// </summary>
    public string? HelpText { get; init; }

    /// <summary>
    /// Creates a single choice question.
    /// </summary>
    public static Question SingleChoice(
        string key,
        string text,
        IReadOnlyList<string> options,
        object? defaultValue = null,
        string? helpText = null) => new()
    {
        Key = key,
        Text = text,
        Type = QuestionType.SingleChoice,
        Options = options,
        DefaultValue = defaultValue,
        HelpText = helpText
    };

    /// <summary>
    /// Creates a multi choice question.
    /// </summary>
    public static Question MultiChoice(
        string key,
        string text,
        IReadOnlyList<string> options,
        object? defaultValue = null,
        string? helpText = null) => new()
    {
        Key = key,
        Text = text,
        Type = QuestionType.MultiChoice,
        Options = options,
        DefaultValue = defaultValue,
        HelpText = helpText
    };

    /// <summary>
    /// Creates a number question.
    /// </summary>
    public static Question Number(
        string key,
        string text,
        object? defaultValue = null,
        string? helpText = null) => new()
    {
        Key = key,
        Text = text,
        Type = QuestionType.Number,
        Options = null,
        DefaultValue = defaultValue,
        HelpText = helpText
    };

    /// <summary>
    /// Creates a yes/no question.
    /// </summary>
    public static Question YesNo(
        string key,
        string text,
        bool? defaultValue = null,
        string? helpText = null) => new()
    {
        Key = key,
        Text = text,
        Type = QuestionType.YesNo,
        Options = null,
        DefaultValue = defaultValue,
        HelpText = helpText
    };

    /// <summary>
    /// Creates a free-text question.
    /// </summary>
    public static Question FreeText(
        string key,
        string text,
        string? defaultValue = null,
        string? helpText = null) => new()
    {
        Key = key,
        Text = text,
        Type = QuestionType.Text,
        Options = null,
        DefaultValue = defaultValue,
        HelpText = helpText
    };
}
