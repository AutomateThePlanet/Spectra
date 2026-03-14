using Spectra.CLI.IO;
using Spectra.CLI.Source;
using Spectra.Core.Index;
using Spectra.Core.Models.Config;
using Spectra.Core.Validation;

namespace Spectra.CLI.Agent.Tools;

/// <summary>
/// Registry that creates and manages all AI agent tools.
/// </summary>
public sealed class ToolRegistry
{
    private readonly string _basePath;
    private readonly SpectraConfig _config;

    private GetDocumentMapTool? _getDocumentMapTool;
    private LoadSourceDocumentTool? _loadSourceDocumentTool;
    private SearchSourceDocsTool? _searchSourceDocsTool;
    private ReadTestIndexTool? _readTestIndexTool;
    private GetNextTestIdsTool? _getNextTestIdsTool;
    private CheckDuplicatesBatchTool? _checkDuplicatesBatchTool;
    private BatchWriteTestsTool? _batchWriteTestsTool;

    public ToolRegistry(string basePath, SpectraConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentNullException.ThrowIfNull(config);

        _basePath = basePath;
        _config = config;
    }

    /// <summary>
    /// Gets the GetDocumentMap tool.
    /// </summary>
    public GetDocumentMapTool GetDocumentMap =>
        _getDocumentMapTool ??= new GetDocumentMapTool(
            new DocumentMapBuilder(_config.Source));

    /// <summary>
    /// Gets the LoadSourceDocument tool.
    /// </summary>
    public LoadSourceDocumentTool LoadSourceDocument =>
        _loadSourceDocumentTool ??= new LoadSourceDocumentTool(
            new SourceDocumentReader(_config.Source));

    /// <summary>
    /// Gets the SearchSourceDocs tool.
    /// </summary>
    public SearchSourceDocsTool SearchSourceDocs =>
        _searchSourceDocsTool ??= new SearchSourceDocsTool(
            new DocumentSearcher(_config.Source));

    /// <summary>
    /// Gets the ReadTestIndex tool.
    /// </summary>
    public ReadTestIndexTool ReadTestIndex =>
        _readTestIndexTool ??= new ReadTestIndexTool();

    /// <summary>
    /// Gets the GetNextTestIds tool with the specified allocator.
    /// </summary>
    public GetNextTestIdsTool GetNextTestIds(TestIdAllocator allocator) =>
        _getNextTestIdsTool ??= new GetNextTestIdsTool(allocator);

    /// <summary>
    /// Gets the CheckDuplicatesBatch tool.
    /// </summary>
    public CheckDuplicatesBatchTool CheckDuplicatesBatch =>
        _checkDuplicatesBatchTool ??= new CheckDuplicatesBatchTool(
            new DuplicateDetector(0.6));

    /// <summary>
    /// Gets the BatchWriteTests tool.
    /// </summary>
    public BatchWriteTestsTool BatchWriteTests =>
        _batchWriteTestsTool ??= new BatchWriteTestsTool(
            new TestFileWriter());

    /// <summary>
    /// Gets all tool definitions for AI function calling.
    /// </summary>
    public IReadOnlyList<ToolDefinition> GetToolDefinitions()
    {
        return
        [
            new ToolDefinition
            {
                Name = GetDocumentMapTool.Name,
                Description = GetDocumentMapTool.Description,
                Parameters = new Dictionary<string, ParameterDefinition>
                {
                    ["basePath"] = new() { Type = "string", Description = "Base path to search for documents", Required = true }
                }
            },
            new ToolDefinition
            {
                Name = LoadSourceDocumentTool.Name,
                Description = LoadSourceDocumentTool.Description,
                Parameters = new Dictionary<string, ParameterDefinition>
                {
                    ["documentPath"] = new() { Type = "string", Description = "Path to the document to load", Required = true },
                    ["sectionHeading"] = new() { Type = "string", Description = "Optional section heading to extract", Required = false }
                }
            },
            new ToolDefinition
            {
                Name = SearchSourceDocsTool.Name,
                Description = SearchSourceDocsTool.Description,
                Parameters = new Dictionary<string, ParameterDefinition>
                {
                    ["basePath"] = new() { Type = "string", Description = "Base path to search", Required = true },
                    ["keyword"] = new() { Type = "string", Description = "Keyword to search for", Required = true },
                    ["maxResults"] = new() { Type = "integer", Description = "Maximum results to return (default: 10)", Required = false }
                }
            },
            new ToolDefinition
            {
                Name = ReadTestIndexTool.Name,
                Description = ReadTestIndexTool.Description,
                Parameters = new Dictionary<string, ParameterDefinition>
                {
                    ["suitePath"] = new() { Type = "string", Description = "Path to the test suite", Required = true }
                }
            },
            new ToolDefinition
            {
                Name = GetNextTestIdsTool.Name,
                Description = GetNextTestIdsTool.Description,
                Parameters = new Dictionary<string, ParameterDefinition>
                {
                    ["count"] = new() { Type = "integer", Description = "Number of IDs to allocate (default: 1)", Required = false }
                }
            },
            new ToolDefinition
            {
                Name = CheckDuplicatesBatchTool.Name,
                Description = CheckDuplicatesBatchTool.Description,
                Parameters = new Dictionary<string, ParameterDefinition>
                {
                    ["proposedTests"] = new() { Type = "array", Description = "Array of proposed test cases", Required = true },
                    ["existingTests"] = new() { Type = "array", Description = "Array of existing test cases", Required = true }
                }
            },
            new ToolDefinition
            {
                Name = BatchWriteTestsTool.Name,
                Description = BatchWriteTestsTool.Description,
                Parameters = new Dictionary<string, ParameterDefinition>
                {
                    ["suitePath"] = new() { Type = "string", Description = "Path to write tests to", Required = true },
                    ["tests"] = new() { Type = "array", Description = "Array of test cases to write", Required = true },
                    ["dryRun"] = new() { Type = "boolean", Description = "If true, don't actually write files", Required = false }
                }
            }
        ];
    }
}

/// <summary>
/// Definition of a tool for AI function calling.
/// </summary>
public sealed record ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Dictionary<string, ParameterDefinition> Parameters { get; init; }
}

/// <summary>
/// Definition of a tool parameter.
/// </summary>
public sealed record ParameterDefinition
{
    public required string Type { get; init; }
    public required string Description { get; init; }
    public bool Required { get; init; }
}
