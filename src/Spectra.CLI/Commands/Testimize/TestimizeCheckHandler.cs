using System.Reflection;
using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Testimize;

/// <summary>
/// Handles <c>spectra testimize check</c>. Reports whether Testimize is
/// enabled, whether the <c>Testimize</c> NuGet assembly is loadable, and
/// what version is bundled.
///
/// v1.48.3: rewritten for the in-process NuGet integration. The previous
/// implementation shelled out to <c>dotnet tool list -g</c> to probe for a
/// global <c>Testimize.MCP.Server</c> tool; that tool is no longer a
/// prerequisite because the Testimize library ships as a direct
/// <c>PackageReference</c> in Spectra.CLI. "Installed" now means
/// "the assembly is present in the process" and is verified by reflecting
/// the type that Spectra.CLI already references at compile time.
/// </summary>
public sealed class TestimizeCheckHandler
{
    private readonly OutputFormat _outputFormat;

    public TestimizeCheckHandler(OutputFormat outputFormat = OutputFormat.Human)
    {
        _outputFormat = outputFormat;
    }

    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");

        // Load config (best effort — if missing or broken, treat as defaults).
        SpectraConfig? config = null;
        if (File.Exists(configPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(configPath, ct);
                config = JsonSerializer.Deserialize<SpectraConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                // Fall through with null config; the report will reflect defaults.
            }
        }

        var testimize = config?.Testimize ?? new TestimizeConfig();

        // The Testimize NuGet assembly is a compile-time dependency of
        // Spectra.CLI — it is always loadable if the CLI itself loaded.
        // We still reflect on it so the version line is accurate to the
        // assembly actually in the process, not a hard-coded constant.
        var (installed, version) = ProbeTestimizeAssembly();

        // "Healthy" used to mean "MCP child process spawned and handshook
        // successfully." Now it simply means "the assembly is loadable and
        // testimize is enabled" — there's no runtime handshake.
        var healthy = installed;

        bool? settingsFileFound = null;
        if (!string.IsNullOrWhiteSpace(testimize.SettingsFile))
        {
            var path = Path.IsPathRooted(testimize.SettingsFile)
                ? testimize.SettingsFile
                : Path.Combine(currentDir, testimize.SettingsFile);
            settingsFileFound = File.Exists(path);
        }

        var result = new TestimizeCheckResult
        {
            Command = "testimize-check",
            Status = "completed",
            Enabled = testimize.Enabled,
            Installed = installed,
            Healthy = testimize.Enabled && healthy,
            Mode = testimize.Mode,
            Strategy = testimize.Strategy,
            SettingsFile = testimize.SettingsFile,
            SettingsFileFound = settingsFileFound,
            Version = version,
        };

        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(result);
        }
        else
        {
            RenderHuman(result);
        }

        return ExitCodes.Success;
    }

    private static (bool Installed, string? Version) ProbeTestimizeAssembly()
    {
        try
        {
            var asm = typeof(global::Testimize.Usage.TestimizeEngine).Assembly;
            return (true, asm.GetName().Version?.ToString());
        }
        catch
        {
            return (false, null);
        }
    }

    private static void RenderHuman(TestimizeCheckResult r)
    {
        Console.WriteLine("Testimize Integration Status");
        Console.WriteLine($"  Enabled:     {(r.Enabled ? "true" : "false")}");
        if (!r.Enabled)
        {
            Console.WriteLine();
            Console.WriteLine("  Testimize is disabled. Set 'testimize.enabled' to true in spectra.config.json to enable.");
            return;
        }

        Console.WriteLine($"  Library:     {(r.Installed ? $"loaded (v{r.Version})" : "NOT LOADED")}");
        Console.WriteLine($"  Ready:       {(r.Healthy ? "✓ yes" : "no")}");
        Console.WriteLine($"  Mode:        {r.Mode}");
        Console.WriteLine($"  Strategy:    {r.Strategy}");
        if (r.SettingsFile is not null)
        {
            var status = r.SettingsFileFound == true ? "found" : "not found";
            Console.WriteLine($"  Settings:    {r.SettingsFile} ({status})");
        }

        Console.WriteLine();
        if (r.Installed && r.Healthy)
        {
            Console.WriteLine("Ready to generate optimized test data.");
        }
        else if (!r.Installed)
        {
            Console.WriteLine("Testimize library not loaded — this should not happen with a properly-installed Spectra.CLI. Reinstall the CLI.");
        }
    }
}
