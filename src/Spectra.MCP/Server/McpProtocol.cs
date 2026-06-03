using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Spectra.MCP.Server;

/// <summary>
/// JSON-RPC 2.0 protocol handling for MCP.
/// </summary>
public static partial class McpProtocol
{
    public const string JsonRpcVersion = "2.0";

    /// <summary>
    /// Options for the JSON-RPC envelope and outgoing responses. Stays lenient:
    /// clients may add envelope-level fields and must not be rejected (Spec 051 D2).
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Options for tool-parameter deserialization. Strict: unmapped members are
    /// rejected (Spec 051) so misplaced/misspelled fields surface as actionable
    /// errors instead of being silently dropped.
    /// </summary>
    private static readonly JsonSerializerOptions ParamsSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>
    /// Parses a JSON-RPC request from a string.
    /// </summary>
    public static McpRequest? ParseRequest(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<McpRequest>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a success response.
    /// </summary>
    public static string CreateResponse<T>(object? id, T result)
    {
        var response = new McpResponse<T>
        {
            JsonRpc = JsonRpcVersion,
            Id = id,
            Result = result
        };

        return JsonSerializer.Serialize(response, SerializerOptions);
    }

    /// <summary>
    /// Creates an error response.
    /// </summary>
    public static string CreateErrorResponse(object? id, int code, string message, object? data = null)
    {
        var response = new McpErrorResponse
        {
            JsonRpc = JsonRpcVersion,
            Id = id,
            Error = new McpError
            {
                Code = code,
                Message = message,
                Data = data
            }
        };

        return JsonSerializer.Serialize(response, SerializerOptions);
    }

    /// <summary>
    /// Deserializes tool parameters with strict unmapped-member handling.
    /// An unmapped (misplaced/misspelled) property throws
    /// <see cref="McpInvalidParamsException"/> with an actionable message naming
    /// the offending property and, for known filter confusions, the correct
    /// field to use (Spec 051). The <paramref name="toolName"/> drives the
    /// suggestion.
    /// </summary>
    public static T? DeserializeParams<T>(JsonElement? paramsElement, string toolName)
    {
        if (paramsElement is null || paramsElement.Value.ValueKind == JsonValueKind.Null)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(paramsElement.Value.GetRawText(), ParamsSerializerOptions);
        }
        catch (JsonException ex) when (TryExtractUnmappedMember(ex.Message, out var property, out var declaringType))
        {
            var suggestion = SuggestFilterField(toolName, property, declaringType);
            throw new McpInvalidParamsException(
                $"Property '{property}' is not valid on '{toolName}'. "
                + (suggestion ?? "Check the tool schema."));
        }
    }

    private static readonly Regex UnmappedMemberRegex = BuildUnmappedMemberRegex();

    [GeneratedRegex(@"The JSON property '([^']+)' could not be mapped to any \.NET member contained in type '([^']+)'")]
    private static partial Regex BuildUnmappedMemberRegex();

    /// <summary>
    /// Extracts the offending property name and its declaring .NET type from the
    /// System.Text.Json unmapped-member exception message.
    /// </summary>
    private static bool TryExtractUnmappedMember(string message, out string property, out string declaringType)
    {
        property = "";
        declaringType = "";
        var match = UnmappedMemberRegex.Match(message);
        if (!match.Success)
        {
            return false;
        }

        property = match.Groups[1].Value;
        // Keep only the short type name (strip namespace/generic decoration).
        var fullType = match.Groups[2].Value;
        var lastDot = fullType.LastIndexOf('.');
        declaringType = lastDot >= 0 ? fullType[(lastDot + 1)..] : fullType;
        return true;
    }

    /// <summary>
    /// Closed suggestion map for the known filter-field confusions (Spec 051 D4).
    /// Returns null for anything outside the map (caller emits a generic message).
    /// </summary>
    private static string? SuggestFilterField(string toolName, string property, string declaringType)
    {
        // A plural filter field nested inside the legacy filters object — point to the top level.
        if (declaringType.Contains("Filters", StringComparison.Ordinal)
            && property is "priorities" or "components" or "tags")
        {
            return $"Use top-level '{property}', not nested under 'filters'.";
        }

        return (toolName, property) switch
        {
            ("start_execution_run", "priority") => "Use 'priorities' (array) at the top level.",
            ("start_execution_run", "component") => "Use 'components' (array) at the top level.",
            ("start_execution_run", "tag") => "Use 'tags' (array) at the top level.",
            ("find_test_cases", "filters") =>
                "find_test_cases uses top-level 'priorities'/'tags'/'components', not a nested 'filters' object.",
            _ => null
        };
    }
}

/// <summary>
/// Thrown when tool parameters contain an unmapped (misplaced or misspelled)
/// property. Caught at the registry boundary and rendered as a structured
/// INVALID_PARAMS error (Spec 051).
/// </summary>
public sealed class McpInvalidParamsException : Exception
{
    public McpInvalidParamsException(string message) : base(message) { }
}

/// <summary>
/// JSON-RPC 2.0 request structure.
/// </summary>
public sealed class McpRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = McpProtocol.JsonRpcVersion;

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("method")]
    public required string Method { get; set; }

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

/// <summary>
/// JSON-RPC 2.0 success response structure.
/// </summary>
public sealed class McpResponse<T>
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = McpProtocol.JsonRpcVersion;

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }
}

/// <summary>
/// JSON-RPC 2.0 error response structure.
/// </summary>
public sealed class McpErrorResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = McpProtocol.JsonRpcVersion;

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("error")]
    public required McpError Error { get; set; }
}

/// <summary>
/// JSON-RPC 2.0 error object.
/// </summary>
public sealed class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

/// <summary>
/// Standard JSON-RPC error codes.
/// </summary>
public static class JsonRpcErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;

    // Application-specific codes (-32000 to -32099)
    public const int InvalidState = -32000;
    public const int NotFound = -32001;
    public const int Conflict = -32002;
    public const int Unauthorized = -32003;
    public const int AuthenticationRequired = -32004;
}
