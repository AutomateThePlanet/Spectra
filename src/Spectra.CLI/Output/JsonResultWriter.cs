using System.Text.Json;
using System.Text.Json.Serialization;
using Spectra.CLI.Results;

namespace Spectra.CLI.Output;

/// <summary>
/// Serializes CommandResult objects to stdout as JSON.
/// </summary>
public static class JsonResultWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Writes a CommandResult as JSON to stdout.
    /// </summary>
    public static void Write(CommandResult result)
    {
        var json = JsonSerializer.Serialize(result, result.GetType(), Options);
        Console.WriteLine(json);
    }

    /// <summary>
    /// Serializes a CommandResult to a JSON string.
    /// </summary>
    public static string Serialize(CommandResult result)
    {
        return JsonSerializer.Serialize(result, result.GetType(), Options);
    }
}
