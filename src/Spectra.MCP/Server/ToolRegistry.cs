using System.Text.Json;

namespace Spectra.MCP.Server;

/// <summary>
/// Registry for MCP tools with dispatch capabilities.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, IMcpTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a tool with the given method name.
    /// </summary>
    public void Register(string method, IMcpTool tool)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentNullException.ThrowIfNull(tool);

        _tools[method] = tool;
    }

    /// <summary>
    /// Checks if a tool is registered for the given method.
    /// </summary>
    public bool HasTool(string method)
    {
        return _tools.ContainsKey(method);
    }

    /// <summary>
    /// Gets all registered tool names.
    /// </summary>
    public IEnumerable<string> GetRegisteredTools()
    {
        return _tools.Keys;
    }

    /// <summary>
    /// Invokes a tool by method name.
    /// </summary>
    public async Task<string> InvokeAsync(string method, JsonElement? parameters)
    {
        if (!_tools.TryGetValue(method, out var tool))
        {
            return McpProtocol.CreateErrorResponse(
                null,
                JsonRpcErrorCodes.MethodNotFound,
                $"Tool not found: {method}");
        }

        try
        {
            return await tool.ExecuteAsync(parameters);
        }
        catch (Exception ex)
        {
            return McpProtocol.CreateErrorResponse(
                null,
                JsonRpcErrorCodes.InternalError,
                $"Tool execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets tool metadata for capability listing.
    /// </summary>
    public IReadOnlyList<ToolMetadata> GetToolMetadata()
    {
        return _tools.Select(kvp => new ToolMetadata
        {
            Name = kvp.Key,
            Description = kvp.Value.Description,
            Parameters = kvp.Value.ParameterSchema
        }).ToList();
    }
}

/// <summary>
/// Interface for MCP tool implementations.
/// </summary>
public interface IMcpTool
{
    /// <summary>
    /// Tool description for capability listing.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// JSON Schema for tool parameters.
    /// </summary>
    object? ParameterSchema { get; }

    /// <summary>
    /// Executes the tool with the given parameters.
    /// </summary>
    /// <param name="parameters">JSON element containing tool parameters.</param>
    /// <returns>JSON-RPC response string.</returns>
    Task<string> ExecuteAsync(JsonElement? parameters);
}

/// <summary>
/// Tool metadata for capability listing.
/// </summary>
public sealed class ToolMetadata
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public object? Parameters { get; init; }
}
