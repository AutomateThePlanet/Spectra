using System.Text.Json;

namespace Spectra.MCP.Server;

/// <summary>
/// MCP Server host with stdio transport.
/// Handles JSON-RPC 2.0 message processing.
/// </summary>
public sealed class McpServer
{
    private readonly ToolRegistry _toolRegistry;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly Infrastructure.McpLogging _logger;

    private bool _running;

    public McpServer(
        ToolRegistry toolRegistry,
        TextReader? input = null,
        TextWriter? output = null,
        Infrastructure.McpLogging? logger = null)
    {
        _toolRegistry = toolRegistry;
        _input = input ?? Console.In;
        _output = output ?? Console.Out;
        _logger = logger ?? new Infrastructure.McpLogging();
    }

    /// <summary>
    /// Starts the server and processes messages until stopped.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _running = true;
        _logger.LogInfo("MCP Server started");

        while (_running && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var line = await _input.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    // End of input stream
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var response = await ProcessMessageAsync(line);
                if (!string.IsNullOrEmpty(response))
                {
                    await _output.WriteLineAsync(response.AsMemory(), cancellationToken);
                    await _output.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing message: {ex.Message}");
                var errorResponse = McpProtocol.CreateErrorResponse(
                    null,
                    JsonRpcErrorCodes.InternalError,
                    ex.Message);
                await _output.WriteLineAsync(errorResponse.AsMemory(), cancellationToken);
            }
        }

        _logger.LogInfo("MCP Server stopped");
    }

    /// <summary>
    /// Stops the server.
    /// </summary>
    public void Stop()
    {
        _running = false;
    }

    /// <summary>
    /// Processes a single JSON-RPC message.
    /// </summary>
    internal async Task<string> ProcessMessageAsync(string message)
    {
        _logger.LogDebug($"Received: {message}");

        var request = McpProtocol.ParseRequest(message);
        if (request is null)
        {
            return McpProtocol.CreateErrorResponse(
                null,
                JsonRpcErrorCodes.ParseError,
                "Invalid JSON");
        }

        if (string.IsNullOrEmpty(request.Method))
        {
            return McpProtocol.CreateErrorResponse(
                request.Id,
                JsonRpcErrorCodes.InvalidRequest,
                "Method is required");
        }

        // Handle protocol methods
        if (request.Method.StartsWith("$/"))
        {
            return HandleProtocolMethod(request);
        }

        // Handle tool invocations
        var response = await _toolRegistry.InvokeAsync(request.Method, request.Params);

        // Wrap result in JSON-RPC response if needed
        if (!response.StartsWith("{\"jsonrpc\""))
        {
            try
            {
                var result = JsonSerializer.Deserialize<object>(response);
                response = McpProtocol.CreateResponse(request.Id, result);
            }
            catch
            {
                // Already a proper response
            }
        }

        _logger.LogDebug($"Response: {response}");
        return response;
    }

    private string HandleProtocolMethod(McpRequest request)
    {
        return request.Method switch
        {
            "$/initialize" => HandleInitialize(request),
            "$/ping" => McpProtocol.CreateResponse(request.Id, new { pong = true }),
            "$/listTools" => HandleListTools(request),
            _ => McpProtocol.CreateErrorResponse(
                request.Id,
                JsonRpcErrorCodes.MethodNotFound,
                $"Unknown protocol method: {request.Method}")
        };
    }

    private string HandleInitialize(McpRequest request)
    {
        var capabilities = new
        {
            name = "spectra-mcp",
            version = "1.0.0",
            tools = _toolRegistry.GetToolMetadata()
        };

        return McpProtocol.CreateResponse(request.Id, capabilities);
    }

    private string HandleListTools(McpRequest request)
    {
        var tools = _toolRegistry.GetToolMetadata();
        return McpProtocol.CreateResponse(request.Id, new { tools });
    }
}
