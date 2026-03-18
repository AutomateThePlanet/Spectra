using Spectra.Core.Models;

namespace Spectra.CLI.Review;

/// <summary>
/// Inline CLI editor for test cases.
/// </summary>
public sealed class TestEditor
{
    /// <summary>
    /// Edits a test case interactively.
    /// </summary>
    /// <param name="test">The test case to edit.</param>
    /// <returns>The edited test case, or null if the user cancels.</returns>
    public TestCase? Edit(TestCase test)
    {
        Console.WriteLine();
        Console.WriteLine("=== Edit Test Case ===");
        Console.WriteLine("(Press Enter to keep current value, 'q' to cancel)");
        Console.WriteLine();

        // Edit title
        Console.WriteLine($"Title [{test.Title}]:");
        var title = Console.ReadLine();
        if (title == "q") return null;
        title = string.IsNullOrWhiteSpace(title) ? test.Title : title.Trim();

        // Edit priority
        Console.WriteLine($"Priority (high/medium/low) [{test.Priority.ToString().ToLowerInvariant()}]:");
        var priorityInput = Console.ReadLine();
        if (priorityInput == "q") return null;
        var priority = ParsePriority(priorityInput, test.Priority);

        // Edit steps
        Console.WriteLine($"Steps (current: {test.Steps.Count} steps):");
        Console.WriteLine("  Enter new steps one per line. Empty line to finish.");
        Console.WriteLine("  Press Enter immediately to keep existing steps.");

        var firstStepInput = Console.ReadLine();
        if (firstStepInput == "q") return null;

        List<string> steps;
        if (string.IsNullOrWhiteSpace(firstStepInput))
        {
            steps = test.Steps.ToList();
        }
        else
        {
            steps = [firstStepInput.Trim()];
            while (true)
            {
                var stepInput = Console.ReadLine();
                if (stepInput == "q") return null;
                if (string.IsNullOrWhiteSpace(stepInput)) break;
                steps.Add(stepInput.Trim());
            }
        }

        // Edit expected result
        Console.WriteLine($"Expected Result [{Truncate(test.ExpectedResult, 50)}]:");
        var expectedResult = Console.ReadLine();
        if (expectedResult == "q") return null;
        expectedResult = string.IsNullOrWhiteSpace(expectedResult)
            ? test.ExpectedResult
            : expectedResult.Trim();

        Console.WriteLine();
        Console.WriteLine("Changes saved.");

        return new TestCase
        {
            Id = test.Id,
            Title = title,
            Priority = priority,
            Tags = test.Tags,
            Component = test.Component,
            Preconditions = test.Preconditions,
            Environment = test.Environment,
            EstimatedDuration = test.EstimatedDuration,
            DependsOn = test.DependsOn,
            SourceRefs = test.SourceRefs,
            RelatedWorkItems = test.RelatedWorkItems,
            Custom = test.Custom,
            Steps = steps,
            ExpectedResult = expectedResult,
            TestData = test.TestData,
            FilePath = test.FilePath
        };
    }

    private static Priority ParsePriority(string? input, Priority defaultValue)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        return input.Trim().ToLowerInvariant() switch
        {
            "high" or "h" => Priority.High,
            "medium" or "m" => Priority.Medium,
            "low" or "l" => Priority.Low,
            _ => defaultValue
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value.Length <= maxLength
            ? value
            : value[..(maxLength - 3)] + "...";
    }
}
