using System.Text.Json;
using Spectra.Core.Index;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.Core.Parsing;
using Spectra.Core.Validation;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tools.Data;

/// <summary>
/// MCP tool that validates test files against the SPECTRA schema.
/// Returns structured validation errors and warnings.
/// </summary>
public sealed class ValidateTestsTool : IMcpTool
{
    private readonly string _basePath;
    private readonly TestValidator _validator;
    private readonly TestCaseParser _parser;

    public string Description => "Validate test files against SPECTRA schema";

    public object? ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            suite = new
            {
                type = "string",
                description = "Suite name to validate (optional, validates all if omitted)"
            }
        }
    };

    public ValidateTestsTool(string basePath, TestValidator? validator = null, TestCaseParser? parser = null)
    {
        _basePath = basePath;
        _validator = validator ?? new TestValidator();
        _parser = parser ?? new TestCaseParser();
    }

    public async Task<string> ExecuteAsync(JsonElement? parameters)
    {
        var suiteName = parameters?.TryGetProperty("suite", out var suiteEl) == true
            ? suiteEl.GetString()
            : null;

        var testsDir = Path.Combine(_basePath, "tests");
        if (!Directory.Exists(testsDir))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "TESTS_DIR_NOT_FOUND",
                "No tests/ directory found in repository root"));
        }

        var suites = GetSuitesToValidate(testsDir, suiteName);
        if (suites.Count == 0 && !string.IsNullOrEmpty(suiteName))
        {
            return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
                "SUITE_NOT_FOUND",
                $"Suite '{suiteName}' not found in tests/ directory"));
        }

        var allErrors = new List<ValidationError>();
        var allWarnings = new List<ValidationWarning>();
        var totalFiles = 0;
        var validFiles = 0;

        foreach (var suiteDir in suites)
        {
            var (errors, warnings, total, valid) = await ValidateSuiteAsync(suiteDir);
            allErrors.AddRange(errors);
            allWarnings.AddRange(warnings);
            totalFiles += total;
            validFiles += valid;
        }

        var result = new
        {
            is_valid = allErrors.Count == 0,
            total_files = totalFiles,
            valid_files = validFiles,
            errors = allErrors.Select(e => new
            {
                code = e.Code,
                message = e.Message,
                file_path = e.FilePath,
                line_number = e.LineNumber,
                field_name = e.FieldName,
                test_id = e.TestId
            }),
            warnings = allWarnings.Select(w => new
            {
                code = w.Code,
                message = w.Message,
                file_path = w.FilePath,
                test_id = w.TestId
            })
        };

        return JsonSerializer.Serialize(McpToolResponse<object>.Success(result));
    }

    private List<string> GetSuitesToValidate(string testsDir, string? suiteName)
    {
        if (!string.IsNullOrEmpty(suiteName))
        {
            var suitePath = Path.Combine(testsDir, suiteName);
            return Directory.Exists(suitePath) ? [suitePath] : [];
        }

        return Directory.GetDirectories(testsDir).ToList();
    }

    private async Task<(List<ValidationError> errors, List<ValidationWarning> warnings, int total, int valid)>
        ValidateSuiteAsync(string suiteDir)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();
        var totalFiles = 0;
        var validFiles = 0;

        var testFiles = Directory.GetFiles(suiteDir, "*.md")
            .Where(f => !Path.GetFileName(f).StartsWith("_"))
            .ToList();

        foreach (var filePath in testFiles)
        {
            totalFiles++;
            var content = await File.ReadAllTextAsync(filePath);
            var relativePath = Path.GetRelativePath(_basePath, filePath);

            var parseResult = _parser.Parse(content, relativePath);
            if (parseResult.IsFailure)
            {
                foreach (var error in parseResult.Errors)
                {
                    errors.Add(new ValidationError(
                        error.Code,
                        error.Message,
                        relativePath,
                        null,
                        error.Line));
                }
                continue;
            }

            var validationResult = _validator.Validate(parseResult.Value);
            if (validationResult.IsValid)
            {
                validFiles++;
            }

            errors.AddRange(validationResult.Errors);
            warnings.AddRange(validationResult.Warnings);
        }

        return (errors, warnings, totalFiles, validFiles);
    }
}
