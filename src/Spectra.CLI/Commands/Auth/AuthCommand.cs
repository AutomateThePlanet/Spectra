using System.CommandLine;
using System.CommandLine.Invocation;
using Spectra.CLI.Options;

namespace Spectra.CLI.Commands.Auth;

/// <summary>
/// The auth command for checking and managing authentication.
/// </summary>
public sealed class AuthCommand : Command
{
    public AuthCommand() : base("auth", "Check authentication status for AI providers")
    {
        var providerOption = new Option<string?>(
            aliases: ["-p", "--provider"],
            description: "Check specific provider (github-models, openai, anthropic)");

        AddOption(providerOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var provider = context.ParseResult.GetValueForOption(providerOption);
            var verbosity = context.ParseResult.GetValueForOption(GlobalOptions.VerbosityOption);

            var handler = new AuthHandler(verbosity);
            context.ExitCode = await handler.ExecuteAsync(provider, context.GetCancellationToken());
        });
    }
}
