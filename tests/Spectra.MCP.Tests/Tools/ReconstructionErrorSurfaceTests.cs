using System.Text.Json;
using Spectra.MCP.Execution;
using Spectra.MCP.Server;

namespace Spectra.MCP.Tests.Tools;

/// <summary>
/// US3 (spec 064): a <see cref="QueueReconstructionException"/> thrown from any tool is surfaced by
/// the central registry dispatch as a distinct <c>RECONSTRUCTION_FAILED</c> error code — never
/// conflated with <c>RUN_NOT_FOUND</c> and never a generic internal error.
/// </summary>
public class ReconstructionErrorSurfaceTests
{
    private sealed class ThrowingTool : IMcpTool
    {
        public string Description => "throws for testing";
        public object? ParameterSchema => null;
        public Task<string> ExecuteAsync(JsonElement? parameters) =>
            throw new QueueReconstructionException("run-x", "orchestration snapshot missing");
    }

    [Fact]
    public async Task QueueReconstructionException_SurfacesAsReconstructionFailed_NotRunNotFound()
    {
        var registry = new ToolRegistry();
        registry.Register("boom", new ThrowingTool());

        var response = await registry.InvokeAsync("boom", null);
        var root = JsonDocument.Parse(response).RootElement;

        var code = root.GetProperty("error").GetProperty("code").GetString();
        Assert.Equal("RECONSTRUCTION_FAILED", code);
        Assert.NotEqual("RUN_NOT_FOUND", code);
    }
}
