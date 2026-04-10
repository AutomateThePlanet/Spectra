using System.Text.Json;
using Spectra.CLI.Agent.Testimize;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Testimize;

/// <summary>
/// Handles `spectra testimize check`. Reports whether Testimize is enabled,
/// installed, and healthy. Spec 038 FR-026 through FR-029.
/// </summary>
public sealed class TestimizeCheckHandler
{
    private const string InstallCommand = "dotnet tool install --global Testimize.MCP.Server";

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

        // FR-028: when disabled, do not start the MCP process.
        bool installed = false;
        bool healthy = false;
        if (testimize.Enabled)
        {
            await using var client = new TestimizeMcpClient();
            installed = await client.StartAsync(testimize.Mcp, ct);
            if (installed)
                healthy = await client.IsHealthyAsync(ct);
        }

        // Resolve settings file presence (only when configured).
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
            Healthy = healthy,
            Mode = testimize.Mode,
            Strategy = testimize.Strategy,
            SettingsFile = testimize.SettingsFile,
            SettingsFileFound = settingsFileFound,
            InstallCommand = (testimize.Enabled && !installed) ? InstallCommand : null
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

        Console.WriteLine($"  MCP Server:  {(r.Installed ? "installed" : "NOT FOUND")}");
        Console.WriteLine($"  Connection:  {(r.Healthy ? "✓ healthy" : "unhealthy")}");
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
            Console.WriteLine($"Install with: {InstallCommand}");
        }
    }
}
