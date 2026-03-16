using System.Text.Json;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Models;
using Spectra.Core.Models.Config;
using Spectra.Core.Models.Profile;
using Spectra.Core.Parsing;
using Spectra.Core.Profile;
using Spectra.Core.Validation;
using ProfileValidationError = Spectra.Core.Models.Profile.ValidationError;

namespace Spectra.CLI.Commands.Validate;

/// <summary>
/// Handles the validate command execution.
/// </summary>
public sealed class ValidateHandler
{
    private readonly VerbosityLevel _verbosity;

    public ValidateHandler(VerbosityLevel verbosity = VerbosityLevel.Normal)
    {
        _verbosity = verbosity;
    }

    public async Task<int> ExecuteAsync(string? suite, CancellationToken cancellationToken = default)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "spectra.config.json");

        // Load config if exists
        SpectraConfig? config = null;
        if (File.Exists(configPath))
        {
            try
            {
                var configJson = await File.ReadAllTextAsync(configPath, cancellationToken);
                config = JsonSerializer.Deserialize<SpectraConfig>(configJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Error reading config: {ex.Message}");
                return ExitCodes.Error;
            }
        }

        // Validate profile if it exists
        var profileValidationErrors = await ValidateProfileAsync(currentDir, cancellationToken);
        var totalProfileErrors = profileValidationErrors.Count;

        var testsDir = config?.Tests?.Dir ?? "tests";
        var testsPath = Path.Combine(currentDir, testsDir);

        if (!Directory.Exists(testsPath))
        {
            Console.Error.WriteLine($"Tests directory not found: {testsPath}");
            Console.Error.WriteLine("Run 'spectra init' to initialize the project.");
            return ExitCodes.Error;
        }

        // Get suites to validate
        var suitesToValidate = GetSuitesToValidate(testsPath, suite);

        if (suitesToValidate.Count == 0)
        {
            if (suite is not null)
            {
                Console.Error.WriteLine($"Suite not found: {suite}");
            }
            else
            {
                Console.Error.WriteLine("No test suites found.");
            }
            return ExitCodes.Error;
        }

        // Parse and validate each suite
        var orchestrator = new ValidationOrchestrator(config?.Validation);
        var parser = new TestCaseParser();
        var allSuites = new List<TestSuite>();
        var totalErrors = 0;
        var totalWarnings = 0;

        foreach (var suitePath in suitesToValidate)
        {
            var suiteName = Path.GetFileName(suitePath);
            var testFiles = Directory.GetFiles(suitePath, "*.md")
                .Where(f => !Path.GetFileName(f).StartsWith("_"))
                .ToList();

            if (testFiles.Count == 0)
            {
                if (_verbosity >= VerbosityLevel.Normal)
                {
                    Console.WriteLine($"Suite '{suiteName}': No test files");
                }
                continue;
            }

            // Parse test files
            var tests = new List<TestCase>();
            foreach (var file in testFiles)
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                var relativePath = Path.GetRelativePath(testsPath, file);
                var parseResult = parser.Parse(content, relativePath);

                if (!parseResult.IsSuccess)
                {
                    Console.Error.WriteLine($"Parse error in {relativePath}:");
                    foreach (var error in parseResult.Errors)
                    {
                        Console.Error.WriteLine($"  [{error.Code}] {error.Message}");
                    }
                    totalErrors += parseResult.Errors.Count;
                    continue;
                }

                tests.Add(parseResult.Value!);
            }

            // Load index if exists
            MetadataIndex? index = null;
            var indexPath = Path.Combine(suitePath, "_index.json");
            if (File.Exists(indexPath))
            {
                try
                {
                    var indexJson = await File.ReadAllTextAsync(indexPath, cancellationToken);
                    index = JsonSerializer.Deserialize<MetadataIndex>(indexJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException)
                {
                    // Index parsing errors will be caught by validation
                }
            }

            var testSuite = new TestSuite
            {
                Name = suiteName,
                Path = suitePath,
                Tests = tests,
                Index = index
            };
            allSuites.Add(testSuite);
        }

        // Run validation
        var result = orchestrator.ValidateAll(allSuites);
        totalErrors += result.Errors.Count + totalProfileErrors;
        totalWarnings += result.Warnings.Count;

        // Output results
        if (result.Errors.Count > 0)
        {
            Console.Error.WriteLine("\nErrors:");
            foreach (var error in result.Errors)
            {
                var location = string.IsNullOrEmpty(error.TestId)
                    ? error.FilePath
                    : $"{error.FilePath} ({error.TestId})";
                Console.Error.WriteLine($"  [{error.Code}] {location}: {error.Message}");
            }
        }

        if (result.Warnings.Count > 0 && _verbosity >= VerbosityLevel.Normal)
        {
            Console.WriteLine("\nWarnings:");
            foreach (var warning in result.Warnings)
            {
                var location = string.IsNullOrEmpty(warning.TestId)
                    ? warning.FilePath
                    : $"{warning.FilePath} ({warning.TestId})";
                Console.WriteLine($"  [{warning.Code}] {location}: {warning.Message}");
            }
        }

        // Summary
        var testCount = allSuites.Sum(s => s.TestCount);
        var suiteCount = allSuites.Count;

        if (_verbosity >= VerbosityLevel.Normal)
        {
            Console.WriteLine();
            Console.WriteLine($"Validated {testCount} test(s) in {suiteCount} suite(s)");
            Console.WriteLine($"Errors: {totalErrors}, Warnings: {totalWarnings}");
        }

        if (result.IsValid)
        {
            if (_verbosity >= VerbosityLevel.Minimal)
            {
                Console.WriteLine("Validation passed.");
            }
            return ExitCodes.Success;
        }

        return ExitCodes.Error;
    }

    private static List<string> GetSuitesToValidate(string testsPath, string? suite)
    {
        if (suite is not null)
        {
            var suitePath = Path.Combine(testsPath, suite);
            return Directory.Exists(suitePath) ? [suitePath] : [];
        }

        return Directory.GetDirectories(testsPath)
            .Where(d => !Path.GetFileName(d).StartsWith("_"))
            .ToList();
    }

    private async Task<List<ProfileValidationError>> ValidateProfileAsync(string basePath, CancellationToken ct)
    {
        var errors = new List<ProfileValidationError>();
        var profilePath = Path.Combine(basePath, ProfileDefaults.RepositoryProfileFileName);

        if (!File.Exists(profilePath))
        {
            return errors;
        }

        try
        {
            var content = await File.ReadAllTextAsync(profilePath, ct);
            var profileValidator = new ProfileValidator();
            var validationResult = profileValidator.ValidateContent(content);

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    Console.Error.WriteLine($"Profile error [{error.Code}]: {error.Message}");
                    errors.Add(error);
                }
            }

            if (validationResult.Warnings.Count > 0 && _verbosity >= VerbosityLevel.Normal)
            {
                foreach (var warning in validationResult.Warnings)
                {
                    Console.WriteLine($"Profile warning [{warning.Code}]: {warning.Message}");
                }
            }

            if (_verbosity >= VerbosityLevel.Detailed && validationResult.IsValid)
            {
                Console.WriteLine($"Profile validated: {profilePath}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading profile: {ex.Message}");
            errors.Add(ProfileValidationError.Create("PROFILE_READ_ERROR", ex.Message));
        }

        return errors;
    }
}
