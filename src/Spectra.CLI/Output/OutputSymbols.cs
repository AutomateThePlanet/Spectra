namespace Spectra.CLI.Output;

/// <summary>
/// Unicode symbols for consistent CLI output.
/// </summary>
public static class OutputSymbols
{
    /// <summary>
    /// Interactive prompt symbol.
    /// </summary>
    public const string Prompt = "◆";

    /// <summary>
    /// Loading/spinner symbol.
    /// </summary>
    public const string Loading = "◐";

    /// <summary>
    /// Success symbol.
    /// </summary>
    public const string Success = "✓";

    /// <summary>
    /// Error symbol.
    /// </summary>
    public const string Error = "✗";

    /// <summary>
    /// Warning symbol.
    /// </summary>
    public const string Warning = "⚠";

    /// <summary>
    /// Info symbol.
    /// </summary>
    public const string Info = "ℹ";

    /// <summary>
    /// Redundant/link symbol.
    /// </summary>
    public const string Link = "↔";

    /// <summary>
    /// Spectre.Console markup for prompt symbol in cyan.
    /// </summary>
    public const string PromptMarkup = "[cyan]◆[/]";

    /// <summary>
    /// Spectre.Console markup for success symbol in green.
    /// </summary>
    public const string SuccessMarkup = "[green]✓[/]";

    /// <summary>
    /// Spectre.Console markup for error symbol in red.
    /// </summary>
    public const string ErrorMarkup = "[red]✗[/]";

    /// <summary>
    /// Spectre.Console markup for warning symbol in yellow.
    /// </summary>
    public const string WarningMarkup = "[yellow]⚠[/]";

    /// <summary>
    /// Spectre.Console markup for info symbol in cyan.
    /// </summary>
    public const string InfoMarkup = "[cyan]ℹ[/]";
}
