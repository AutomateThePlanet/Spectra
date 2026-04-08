using Spectra.Core.Models.Coverage;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Spectra.Core.Parsing;

/// <summary>
/// Parses acceptance criteria YAML files into AcceptanceCriterion objects.
/// Supports both the new "criteria" key and the legacy "requirements" key.
/// </summary>
public sealed class AcceptanceCriteriaParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Parses acceptance criteria from a YAML file. Returns empty list if file doesn't exist or is malformed.
    /// </summary>
    public async Task<IReadOnlyList<AcceptanceCriterion>> ParseAsync(
        string filePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, ct);
            return Parse(content);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Parses acceptance criteria from YAML content.
    /// Tries the new "criteria" key first, then falls back to the legacy "requirements" key.
    /// </summary>
    public IReadOnlyList<AcceptanceCriterion> Parse(string yamlContent)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            return [];
        }

        try
        {
            // Try new format first (criteria key)
            var criteriaDoc = Deserializer.Deserialize<CriteriaDocument>(yamlContent);
            if (criteriaDoc?.Criteria is { Count: > 0 })
            {
                return criteriaDoc.Criteria;
            }

            // Fall back to legacy format (requirements key) and convert
#pragma warning disable CS0618 // Obsolete types used for backward compatibility
            var reqDoc = Deserializer.Deserialize<RequirementsDocument>(yamlContent);
            if (reqDoc?.Requirements is { Count: > 0 })
            {
                return reqDoc.Requirements.Select(ConvertFromRequirement).ToList();
            }
#pragma warning restore CS0618

            return [];
        }
        catch
        {
            return [];
        }
    }

#pragma warning disable CS0618 // Obsolete type used for backward compatibility
    private static AcceptanceCriterion ConvertFromRequirement(RequirementDefinition req)
    {
        return new AcceptanceCriterion
        {
            Id = req.Id,
            Text = req.Title,
            Source = req.Source,
            Priority = req.Priority ?? "medium"
        };
    }
#pragma warning restore CS0618
}
