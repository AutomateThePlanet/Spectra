using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spectra.MCP.Server;

/// <summary>
/// JSON-RPC 2.0 protocol handling for MCP.
/// </summary>
public static class McpProtocol
{
    public const string JsonRpcVersion = "2.0";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
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
    /// Serializes parameters to JSON element for tool dispatch.
    /// </summary>
    public static T? DeserializeParams<T>(JsonElement? paramsElement)
    {
        if (paramsElement is null || paramsElement.Value.ValueKind == JsonValueKind.Null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(paramsElement.Value.GetRawText(), SerializerOptions);
    }
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
