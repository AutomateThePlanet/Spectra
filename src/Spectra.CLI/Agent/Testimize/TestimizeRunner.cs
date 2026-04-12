using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Testimize;
using Testimize.OutputGenerators;
using Testimize.Parameters.Core;
using Testimize.Usage;

namespace Spectra.CLI.Agent.Testimize;

/// <summary>
/// In-process orchestration layer around the Testimize NuGet library. Called
/// once per suite from <see cref="Spectra.CLI.Commands.Generate.GenerateHandler"/>
/// between behavior analysis and the batch generation loop, replacing the
/// MCP-based child-process integration deleted in v1.48.3.
///
/// Maps a list of <see cref="FieldSpec"/> values (produced by the AI behavior
/// analysis step, or by <see cref="FieldSpecAnalysisTools.Analyze"/> as a
/// regex fallback) into Testimize's <c>TestimizeInputBuilder</c>, runs the
/// engine with the configured strategy/mode, and projects the result into a
/// <see cref="TestimizeDataset"/> suitable for embedding in the test
/// generation prompt.
/// </summary>
public static class TestimizeRunner
{
    /// <summary>
    /// Produces algorithmic test data for a suite. Returns null when
    /// Testimize is disabled, no usable field specs are available, or the
    /// engine throws. All failure modes log a diagnostic line and never
    /// block generation.
    /// </summary>
    public static TestimizeDataset? Generate(
        IReadOnlyList<FieldSpec>? fieldSpecs,
        TestimizeConfig config,
        IReadOnlyList<SourceDocument>? fallbackDocs,
        string suiteName,
        Action<string>? onStatus = null)
    {
        if (config is null || !config.Enabled)
        {
            DebugLogger.Append("generate", $"TESTIMIZE SKIP reason=disabled suite={suiteName}");
            return null;
        }

        var specs = (fieldSpecs ?? []).ToList();

        // Regex fallback when the AI step surfaced zero field specs.
        if (specs.Count == 0 && fallbackDocs is { Count: > 0 })
        {
            var regexSpecs = FieldSpecAnalysisTools.Analyze(fallbackDocs);
            if (regexSpecs.Count > 0)
            {
                DebugLogger.Append("generate", $"TESTIMIZE FALLBACK source=regex fields={regexSpecs.Count} suite={suiteName}");
                specs = regexSpecs;
            }
        }

        // Drop specs whose type we can't map onto a TestimizeInputBuilder.Add* call.
        var usable = specs.Where(IsMappableType).ToList();

        if (usable.Count == 0)
        {
            DebugLogger.Append("generate", $"TESTIMIZE SKIP reason=no_field_specs suite={suiteName}");
            return null;
        }

        // Testimize's generators (Pairwise, Hybrid-ABC via Pairwise seeding,
        // Combinatorial) all require ≥ 2 parameters to run — single-field
        // input raises ArgumentException("Pairwise testing requires at
        // least two parameters.") deep in the strategy. Skip cleanly; the
        // AI still generates normal tests for that suite without embedded
        // algorithmic data.
        if (usable.Count < 2)
        {
            DebugLogger.Append("generate", $"TESTIMIZE SKIP reason=insufficient_fields fields={usable.Count} suite={suiteName}");
            return null;
        }

        var sw = Stopwatch.StartNew();
        var mode = MapStrategy(config.Strategy);
        var category = MapMode(config.Mode);

        try
        {
            onStatus?.Invoke($"Running Testimize ({usable.Count} fields, strategy={mode})...");

            // The Testimize library unconditionally writes its output
            // generator result to Console.Out and attempts to copy it to
            // the clipboard — both would corrupt --output-format json and
            // fail on headless CI. Redirect stdout for the duration of the
            // Generate() call; everything useful comes back as the List<TestCase>
            // return value.
            var origOut = Console.Out;
            List<global::Testimize.Parameters.Core.TestCase> testCases;
            try
            {
                Console.SetOut(TextWriter.Null);

                var builder = TestimizeEngine.Configure(
                    parameters =>
                    {
                        foreach (var spec in usable)
                            ApplySpec(parameters, spec);
                    },
                    overrides =>
                    {
                        overrides.Mode = mode;
                        overrides.TestCaseCategory = category;
                        overrides.MethodName = SanitizeMethodName(suiteName);
                        ApplyAbcSettings(overrides, config);
                    });

                testCases = builder.Generate();
            }
            finally
            {
                Console.SetOut(origOut);
            }
            sw.Stop();

            var dataset = Project(testCases, usable, mode);

            DebugLogger.Append(
                "generate",
                $"TESTIMIZE OK strategy={mode} fields={usable.Count} test_data_sets={dataset.TestCases.Count} elapsed={sw.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)}s");

            return dataset;
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.Append(
                "generate",
                $"TESTIMIZE ERROR exception={ex.GetType().Name} message=\"{ex.Message}\" elapsed={sw.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)}s");
            return null;
        }
    }

    internal static TestGenerationMode MapStrategy(string? s) => s?.ToLowerInvariant() switch
    {
        "pairwise" => TestGenerationMode.Pairwise,
        "optimizedpairwise" => TestGenerationMode.OptimizedPairwise,
        "combinatorial" => TestGenerationMode.Combinatorial,
        "optimizedcombinatorial" => TestGenerationMode.OptimizedCombinatorial,
        _ => TestGenerationMode.HybridArtificialBeeColony,
    };

    internal static TestCaseCategory MapMode(string? m) => m?.ToLowerInvariant() switch
    {
        "valid" => TestCaseCategory.Valid,
        "validation" or "invalid" => TestCaseCategory.Validation,
        _ => TestCaseCategory.All,
    };

    internal static bool IsMappableType(FieldSpec spec) => spec.Type?.ToLowerInvariant() switch
    {
        "integer" or "int" or "number" => true,
        "text" or "string" => true,
        "email" => true,
        "phone" => true,
        "password" => true,
        "url" => true,
        "username" => true,
        "date" => true,
        "boolean" or "bool" => true,
        "singleselect" or "select" or "dropdown" or "enum" => spec.AllowedValues is { Count: > 0 },
        "multiselect" => spec.AllowedValues is { Count: > 0 },
        _ => false,
    };

    private static void ApplySpec(TestimizeInputBuilder parameters, FieldSpec spec)
    {
        switch (spec.Type?.ToLowerInvariant())
        {
            case "integer":
            case "int":
            case "number":
                {
                    var min = (int)(spec.Min ?? 0);
                    var max = (int)(spec.Max ?? (min + 100));
                    parameters.AddInteger(min, max);
                    break;
                }

            case "text":
            case "string":
                parameters.AddText(spec.MinLength ?? 1, spec.MaxLength ?? 100);
                break;

            case "email":
                parameters.AddEmail(spec.MinLength ?? 6, spec.MaxLength ?? 254);
                break;

            case "phone":
                parameters.AddPhone(spec.MinLength ?? 7, spec.MaxLength ?? 15);
                break;

            case "password":
                parameters.AddPassword(spec.MinLength ?? 8, spec.MaxLength ?? 64);
                break;

            case "url":
                parameters.AddUrl(spec.MinLength ?? 10, spec.MaxLength ?? 2048);
                break;

            case "username":
                parameters.AddUsername(spec.MinLength ?? 3, spec.MaxLength ?? 32);
                break;

            case "date":
                {
                    var min = ParseDate(spec.MinDate, DateTime.Parse("1900-01-01", CultureInfo.InvariantCulture));
                    var max = ParseDate(spec.MaxDate, DateTime.Parse("2100-12-31", CultureInfo.InvariantCulture));
                    parameters.AddDate(min, max);
                    break;
                }

            case "boolean":
            case "bool":
                parameters.AddBoolean();
                break;

            case "singleselect":
            case "select":
            case "dropdown":
            case "enum":
                parameters.AddSingleSelect(b =>
                {
                    foreach (var v in spec.AllowedValues!)
                        b.Valid(v);
                    return b;
                });
                break;

            case "multiselect":
                parameters.AddMultiSelect(b =>
                {
                    foreach (var v in spec.AllowedValues!)
                        b.Valid(new[] { v });
                    return b;
                });
                break;
        }
    }

    private static DateTime ParseDate(string? iso, DateTime fallback)
    {
        if (string.IsNullOrWhiteSpace(iso))
            return fallback;
        return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
            ? d
            : fallback;
    }

    private static void ApplyAbcSettings(PreciseTestEngineSettings overrides, TestimizeConfig config)
    {
        // Optional settings file — JSON with the same abc_settings keys as the config class.
        if (!string.IsNullOrWhiteSpace(config.SettingsFile) && File.Exists(config.SettingsFile))
        {
            try
            {
                var json = File.ReadAllText(config.SettingsFile);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("abc_settings", out var abcEl) ||
                    doc.RootElement.TryGetProperty("abcGenerationSettings", out abcEl) ||
                    (doc.RootElement.TryGetProperty("testimizeSettings", out var ts) &&
                     ts.TryGetProperty("abcGenerationSettings", out abcEl)))
                {
                    overrides.ABCSettings ??= new global::Testimize.ABCGenerationSettings();
                    ApplyAbcJson(overrides.ABCSettings, abcEl);
                }
            }
            catch
            {
                // Fall through — inline config still applies below.
            }
        }

        if (config.AbcSettings is { } cfgAbc)
        {
            overrides.ABCSettings ??= new global::Testimize.ABCGenerationSettings();
            overrides.ABCSettings.TotalPopulationGenerations = cfgAbc.TotalPopulationGenerations;
            overrides.ABCSettings.MutationRate = cfgAbc.MutationRate;
            overrides.ABCSettings.FinalPopulationSelectionRatio = cfgAbc.FinalPopulationSelectionRatio;
            overrides.ABCSettings.EliteSelectionRatio = cfgAbc.EliteSelectionRatio;
            overrides.ABCSettings.AllowMultipleInvalidInputs = cfgAbc.AllowMultipleInvalidInputs;
            if (cfgAbc.Seed is { } seed) overrides.ABCSettings.Seed = seed;
        }
    }

    private static void ApplyAbcJson(global::Testimize.ABCGenerationSettings settings, JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return;
        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name.ToLowerInvariant())
            {
                case "totalpopulationgenerations": if (prop.Value.TryGetInt32(out var tpg)) settings.TotalPopulationGenerations = tpg; break;
                case "mutationrate": if (prop.Value.TryGetDouble(out var mr)) settings.MutationRate = mr; break;
                case "seed": if (prop.Value.TryGetInt32(out var seed)) settings.Seed = seed; break;
                case "finalpopulationselectionratio": if (prop.Value.TryGetDouble(out var fps)) settings.FinalPopulationSelectionRatio = fps; break;
                case "eliteselectionratio": if (prop.Value.TryGetDouble(out var esr)) settings.EliteSelectionRatio = esr; break;
                case "onlookerselectionratio": if (prop.Value.TryGetDouble(out var osr)) settings.OnlookerSelectionRatio = osr; break;
                case "scoutselectionratio": if (prop.Value.TryGetDouble(out var ssr)) settings.ScoutSelectionRatio = ssr; break;
                case "coolingrate": if (prop.Value.TryGetDouble(out var cr)) settings.CoolingRate = cr; break;
                case "stagnationthresholdpercentage": if (prop.Value.TryGetDouble(out var stp)) settings.StagnationThresholdPercentage = stp; break;
            }
        }
    }

    private static TestimizeDataset Project(
        IReadOnlyList<global::Testimize.Parameters.Core.TestCase> testCases,
        IReadOnlyList<FieldSpec> specs,
        TestGenerationMode strategy)
    {
        var rows = new List<TestimizeRow>(testCases.Count);
        foreach (var tc in testCases)
        {
            var cells = new List<TestimizeCell>(tc.Values.Count);
            for (var i = 0; i < tc.Values.Count; i++)
            {
                var value = tc.Values[i];
                var name = i < specs.Count ? specs[i].Name : $"field{i + 1}";
                cells.Add(new TestimizeCell
                {
                    FieldName = string.IsNullOrWhiteSpace(name) ? $"field{i + 1}" : name,
                    Value = value.Value,
                    Category = value.Category.ToString(),
                    ExpectedInvalidMessage = value.ExpectedInvalidMessage,
                });
            }
            rows.Add(new TestimizeRow { Values = cells, Score = tc.Score });
        }

        return new TestimizeDataset
        {
            Strategy = strategy.ToString(),
            FieldCount = specs.Count,
            Fields = specs,
            TestCases = rows,
        };
    }

    private static string SanitizeMethodName(string suite)
    {
        if (string.IsNullOrWhiteSpace(suite)) return "GeneratedTest";
        var chars = suite.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "GeneratedTest" : new string(chars);
    }
}
