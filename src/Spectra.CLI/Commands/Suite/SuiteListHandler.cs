using System.Text.Json;
using System.Text.RegularExpressions;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.CLI.Output;
using Spectra.CLI.Results;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Commands.Suite;

/// <summary>
/// Spec 040: implements <c>spectra suite list</c>.
/// </summary>
public sealed class SuiteListHandler
{
    private readonly VerbosityLevel _verbosity;
    private readonly OutputFormat _outputFormat;

    public SuiteListHandler(VerbosityLevel verbosity = VerbosityLevel.Normal, OutputFormat outputFormat = OutputFormat.Human)
    {
        _verbosity = verbosity;
        _outputFormat = outputFormat;
    }

    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var workspace = Directory.GetCurrentDirectory();
        var testCasesDir = ResolveTestCasesDir(workspace);

        var suites = new List<SuiteListEntry>();
        if (Directory.Exists(testCasesDir))
        {
            foreach (var dir in Directory.EnumerateDirectories(testCasesDir))
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(dir);
                var (testCount, automatedCount) = await CountAsync(dir, ct).ConfigureAwait(false);
                suites.Add(new SuiteListEntry
                {
                    Name = name,
                    TestCount = testCount,
                    AutomatedCount = automatedCount,
                    Directory = Path.GetRelativePath(workspace, dir).Replace('\\', '/')
                });
            }
        }

        var result = new SuiteListResult
        {
            Command = "suite list",
            Status = "completed",
            Suites = suites.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList()
        };

        if (_outputFormat == OutputFormat.Human && _verbosity != VerbosityLevel.Quiet)
        {
            if (suites.Count == 0)
            {
                Console.WriteLine("No suites found.");
            }
            else
            {
                Console.WriteLine($"{"Suite",-30} {"Tests",6} {"Automated",10}");
                foreach (var s in result.Suites)
                {
                    Console.WriteLine($"{s.Name,-30} {s.TestCount,6} {s.AutomatedCount,10}");
                }
            }
        }

        EmitResult(result);
        return ExitCodes.Success;
    }

    private static async Task<(int TestCount, int AutomatedCount)> CountAsync(string suiteDir, CancellationToken ct)
    {
        var tests = 0;
        var automated = 0;

        foreach (var file in Directory.EnumerateFiles(suiteDir, "*.md", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            if (name.StartsWith('_'))
            {
                continue;
            }
            tests++;

            try
            {
                var content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                if (Regex.IsMatch(content, @"automated_by:\s*\n\s+-\s", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(content, @"automated_by:\s+\S", RegexOptions.IgnoreCase))
                {
                    automated++;
                }
            }
            catch (IOException) { /* skip */ }
        }

        return (tests, automated);
    }

    private static string ResolveTestCasesDir(string workspace)
    {
        var configPath = Path.Combine(workspace, "spectra.config.json");
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<SpectraConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (!string.IsNullOrWhiteSpace(config?.Tests?.Dir))
                {
                    return Path.Combine(workspace, config.Tests.Dir);
                }
            }
            catch { /* fall through */ }
        }
        return Path.Combine(workspace, "test-cases");
    }

    private void EmitResult(CommandResult result)
    {
        if (_outputFormat == OutputFormat.Json)
        {
            JsonResultWriter.Write(result);
        }
        var resultPath = Path.Combine(Directory.GetCurrentDirectory(), ".spectra-result.json");
        try
        {
            using var fs = new FileStream(resultPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fs);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            writer.Write(JsonSerializer.Serialize(result, result.GetType(), options));
            writer.Flush();
            fs.Flush(true);
        }
        catch { /* non-critical */ }
    }
}
