using Spectra.CLI.Agent;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Auth;

/// <summary>
/// Handles the auth command for checking authentication status.
/// </summary>
public sealed class AuthHandler
{
    private readonly VerbosityLevel _verbosity;

    public AuthHandler(VerbosityLevel verbosity = VerbosityLevel.Normal)
    {
        _verbosity = verbosity;
    }

    public async Task<int> ExecuteAsync(string? provider, CancellationToken ct = default)
    {
        Console.WriteLine("SPECTRA Authentication Status");
        Console.WriteLine(new string('=', 40));
        Console.WriteLine();

        var providers = string.IsNullOrEmpty(provider)
            ? AgentFactory.GetAvailableProviders().Where(p => p != "mock").ToList()
            : new List<string> { provider };

        var hasErrors = false;

        foreach (var providerName in providers)
        {
            var authResult = await AgentFactory.GetAuthStatusAsync(providerName, null, ct);
            WriteProviderStatus(providerName, authResult);

            if (!authResult.IsAuthenticated)
            {
                hasErrors = true;
            }
        }

        return hasErrors ? ExitCodes.Error : ExitCodes.Success;
    }

    private void WriteProviderStatus(string providerName, AuthResult authResult)
    {
        var displayName = GetProviderDisplayName(providerName);
        var padding = Math.Max(0, 20 - displayName.Length);

        if (authResult.IsAuthenticated)
        {
            Console.WriteLine($"{displayName}{new string(' ', padding)}[OK] via {authResult.Source}");
        }
        else
        {
            Console.WriteLine($"{displayName}{new string(' ', padding)}[NOT CONFIGURED]");

            foreach (var instruction in authResult.SetupInstructions)
            {
                if (string.IsNullOrEmpty(instruction))
                {
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine($"{new string(' ', 22)}{instruction}");
                }
            }
        }

        Console.WriteLine();
    }

    private static string GetProviderDisplayName(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "github-models" => "github-models",
            "openai" => "openai",
            "anthropic" => "anthropic",
            _ => providerName
        };
    }

    /// <summary>
    /// Writes an authentication error message to the console.
    /// </summary>
    public static void WriteAuthError(string providerName, AuthResult authResult)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Authentication failed for provider '{providerName}'.");
        Console.Error.WriteLine();

        foreach (var instruction in authResult.SetupInstructions)
        {
            if (string.IsNullOrEmpty(instruction))
            {
                Console.Error.WriteLine();
            }
            else
            {
                Console.Error.WriteLine($"  {instruction}");
            }
        }

        Console.Error.WriteLine();
    }
}
