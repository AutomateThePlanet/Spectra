namespace Spectra.Core.Models.Profile;

/// <summary>
/// Type of questionnaire question response expected.
/// </summary>
public enum QuestionType
{
    /// <summary>
    /// Select one from options.
    /// </summary>
    SingleChoice,

    /// <summary>
    /// Select multiple from options.
    /// </summary>
    MultiChoice,

    /// <summary>
    /// Enter a numeric value.
    /// </summary>
    Number,

    /// <summary>
    /// Boolean yes/no.
    /// </summary>
    YesNo,

    /// <summary>
    /// Free-form text input.
    /// </summary>
    Text
}
