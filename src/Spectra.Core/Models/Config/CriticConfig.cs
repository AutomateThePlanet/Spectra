using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Spectra.Core.Models.Config;

/// <summary>
/// Spec 097 (FR-020): the optional <c>ai.critic</c> block — which model the <c>spectra-critic</c> subagent runs on.
/// <c>init</c> and <c>update-skills</c> write it into the installed agent's frontmatter; Claude Code reads it from
/// there. Spec 098 (F1): a block that still carries a key of the removed in-process critic (<see cref="RetiredKeyNames"/>)
/// is legacy — its <c>model</c> was a provider model and is ignored — and an invalid <c>model</c> is ignored with a
/// warning. Any JSON value is accepted here (<see cref="CriticConfigConverter"/>), so no command fails on this block.
/// </summary>
[JsonConverter(typeof(CriticConfigConverter))]
public sealed partial class CriticConfig
{
    /// <summary>The model when <c>ai.critic.model</c> (or <c>ai.critic</c>, or <c>ai</c>) is missing or ignored.</summary>
    public const string DefaultModel = "sonnet";

    /// <summary>Spec 098 (FR-001): the keys of the removed in-process critic; any of them makes the block legacy.</summary>
    public static readonly IReadOnlyList<string> RetiredKeyNames =
        ["provider", "api_key_env", "base_url", "enabled", "timeout_seconds", "max_concurrent"];

    /// <summary>
    /// An alias (<c>sonnet</c>, <c>opus</c>, <c>haiku</c>), a model id, or <c>inherit</c>: one token that starts with a
    /// letter or digit, so the unquoted frontmatter value is always a YAML string. Null when absent or not a string.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>Spec 098: the retired keys the block carries (lowercase, in <see cref="RetiredKeyNames"/> order).</summary>
    [JsonIgnore]
    public IReadOnlyList<string> RetiredKeys { get; init; } = [];

    /// <summary>Spec 098: the raw JSON of a <c>model</c> that is present but not a string (e.g. <c>5</c>), else null.</summary>
    [JsonIgnore]
    public string? NonStringModel { get; init; }

    /// <summary>Spec 098: <c>ai.critic</c> was present but not an object (its raw JSON), else null.</summary>
    [JsonIgnore]
    public string? NotAnObject { get; init; }

    /// <summary>Spec 098: the block has a retired key.</summary>
    [JsonIgnore]
    public bool IsLegacy => RetiredKeys.Count > 0;

    /// <summary>Spec 097 (FR-020): first a letter or digit, then letters, digits, <c>._:[]-</c>; at most 200 characters.</summary>
    public static bool IsValidModel(string? model) => model is not null && ModelPattern().IsMatch(model);

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._:\[\]-]{0,199}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ModelPattern();

    /// <summary>
    /// Spec 098 (FR-001, FR-002): the model the critic subagent runs on, and the warning to show when the configured
    /// value was ignored (null when nothing was ignored).
    /// </summary>
    public CriticModelChoice Choose()
    {
        if (NotAnObject is not null)
        {
            return new CriticModelChoice(DefaultModel,
                $"ai.critic must be an object with a model (got {NotAnObject}); using {DefaultModel}");
        }

        if (IsLegacy)
        {
            return new CriticModelChoice(DefaultModel, Model is null && NonStringModel is null ? null : LegacyModelWarning);
        }

        if (NonStringModel is not null || (Model is not null && !IsValidModel(Model)))
        {
            var shown = NonStringModel ?? JsonSerializer.Serialize(Model);
            return new CriticModelChoice(DefaultModel,
                $"ai.critic.model {shown} is not valid (one word: an alias such as sonnet, a model id, or inherit); using {DefaultModel}");
        }

        return new CriticModelChoice(Model ?? DefaultModel, null);
    }

    /// <summary>Spec 098 (FR-003): the notice for a legacy block's <c>model</c>.</summary>
    public const string LegacyModelWarning =
        "ai.critic.model from the old in-process critic is ignored; set it again (alias, model id or inherit) to choose the critic subagent's model";
}

/// <summary>Spec 098: the critic's model and, when the configured value was ignored, why.</summary>
public sealed record CriticModelChoice(string Model, string? Warning);

/// <summary>
/// Spec 098 (F1): reads any JSON value for <c>ai.critic</c> without throwing — an object gives its string <c>model</c>
/// and the retired keys it has (case-insensitive, like the rest of the config); a non-string <c>model</c> or a
/// non-object block is recorded for the warning. Writes <c>{"model": …}</c>.
/// </summary>
public sealed class CriticConfigConverter : JsonConverter<CriticConfig>
{
    public override CriticConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return new CriticConfig { NotAnObject = root.GetRawText() };
        }

        string? model = null;
        string? nonString = null;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            keys.Add(property.Name);
            if (!string.Equals(property.Name, "model", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            switch (property.Value.ValueKind)
            {
                case JsonValueKind.String:
                    model = property.Value.GetString();
                    nonString = null;
                    break;
                case JsonValueKind.Null:
                    model = null;
                    nonString = null;
                    break;
                default:
                    model = null;
                    nonString = property.Value.GetRawText();
                    break;
            }
        }

        return new CriticConfig
        {
            Model = model,
            NonStringModel = nonString,
            RetiredKeys = CriticConfig.RetiredKeyNames.Where(keys.Contains).ToList()
        };
    }

    public override void Write(Utf8JsonWriter writer, CriticConfig value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Model is not null)
        {
            writer.WriteString("model", value.Model);
        }

        writer.WriteEndObject();
    }
}
