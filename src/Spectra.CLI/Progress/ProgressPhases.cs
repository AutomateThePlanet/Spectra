namespace Spectra.CLI.Progress;

/// <summary>
/// Static phase definitions for each command type's progress stepper.
/// </summary>
public static class ProgressPhases
{
    public static readonly string[] Generate =
        ["analyzing", "analyzed", "generating", "completed"];

    public static readonly string[] Update =
        ["classifying", "updating", "verifying", "completed"];

    public static readonly string[] DocsIndex =
        ["scanning", "indexing", "extracting-criteria", "completed"];

    public static readonly string[] Coverage =
        ["scanning-tests", "analyzing-docs", "analyzing-criteria", "analyzing-automation", "completed"];

    public static readonly string[] ExtractCriteria =
        ["scanning-docs", "extracting", "building-index", "completed"];

    public static readonly string[] Dashboard =
        ["collecting-data", "generating-html", "completed"];
}
