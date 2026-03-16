using System.CommandLine;
using System.CommandLine.Invocation;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Profile;
using Spectra.Core.Models.Profile;
using Spectra.Core.Profile;

namespace Spectra.CLI.Commands;

/// <summary>
/// Command for viewing and managing test generation profiles.
/// </summary>
public sealed class ProfileCommand : Command
{
    public ProfileCommand() : base("profile", "View and manage test generation profiles")
    {
        AddCommand(new ShowCommand());
    }

    private sealed class ShowCommand : Command
    {
        private static readonly Option<string?> SuiteOption = new(
            ["--suite", "-s"],
            "Show the effective profile for a specific suite");

        private static readonly Option<bool> JsonOption = new(
            ["--json", "-j"],
            "Output profile as JSON");

        private static readonly Option<bool> ContextOption = new(
            ["--context", "-c"],
            "Show the AI context that would be generated");

        public ShowCommand() : base("show", "Display the current effective profile")
        {
            AddOption(SuiteOption);
            AddOption(JsonOption);
            AddOption(ContextOption);

            this.SetHandler(ExecuteAsync);
        }

        private async Task ExecuteAsync(InvocationContext context)
        {
            var ct = context.GetCancellationToken();

            var suiteDir = context.ParseResult.GetValueForOption(SuiteOption);
            var asJson = context.ParseResult.GetValueForOption(JsonOption);
            var showContext = context.ParseResult.GetValueForOption(ContextOption);

            try
            {
                var basePath = Directory.GetCurrentDirectory();
                var suitePath = suiteDir is not null
                    ? Path.IsPathRooted(suiteDir) ? suiteDir : Path.Combine(basePath, suiteDir)
                    : null;

                var loader = new ProfileLoader();
                var effective = await loader.LoadAsync(basePath, suitePath, ct);

                if (effective.Source.Type == SourceType.Default)
                {
                    var renderer = new ProfileRenderer();
                    Console.WriteLine(renderer.FormatNoProfile());
                    context.ExitCode = ExitCodes.Success;
                    return;
                }

                if (asJson)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(effective.Profile, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
                    });
                    Console.WriteLine(json);
                }
                else if (showContext)
                {
                    var contextBuilder = new ProfileContextBuilder();
                    var aiContext = contextBuilder.Build(effective);
                    Console.WriteLine(aiContext);
                }
                else
                {
                    var renderer = new ProfileRenderer();
                    Console.WriteLine(renderer.Format(effective));
                }

                context.ExitCode = ExitCodes.Success;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nOperation cancelled.");
                context.ExitCode = ExitCodes.Cancelled;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                context.ExitCode = ExitCodes.Error;
            }
        }

    }
}
