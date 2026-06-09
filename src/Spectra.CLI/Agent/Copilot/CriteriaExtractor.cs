using System.Diagnostics;
using GitHub.Copilot.SDK;
using Spectra.CLI.Extraction;
using Spectra.CLI.Prompts;
using Spectra.CLI.Services;
using Spectra.Core.Models.Coverage;
using SpectraProviderConfig = Spectra.Core.Models.Config.ProviderConfig;

namespace Spectra.CLI.Agent.Copilot;

/// <summary>
/// Extracts acceptance criteria from a single document using the Copilot SDK.
/// Per-document extraction eliminates truncation issues.
/// </summary>
public sealed class CriteriaExtractor
{
    private readonly SpectraProviderConfig _provider;
    private readonly Action<string>? _onStatus;
    private readonly TokenUsageTracker? _tracker;

    public CriteriaExtractor(
        SpectraProviderConfig provider,
        Action<string>? onStatus = null,
        TokenUsageTracker? tracker = null)
    {
        _provider = provider;
        _onStatus = onStatus;
        _tracker = tracker;
    }

    private void RecordAndLog(
        string message,
        TimeSpan elapsed,
        int? tokensIn,
        int? tokensOut,
        bool estimated)
    {
        Spectra.CLI.Infrastructure.DebugLogger.AppendAi(
            "criteria",
            message,
            _provider?.Model,
            _provider?.Name,
            tokensIn,
            tokensOut,
            estimated);
        _tracker?.Record("criteria", _provider?.Model ?? "", _provider?.Name ?? "", tokensIn, tokensOut, elapsed, estimated);
    }

    /// <summary>
    /// Extracts acceptance criteria from a single document.
    /// Spec 047: returns a typed <see cref="CriteriaExtractionResult"/> so the
    /// caller can distinguish a genuine extraction (cacheable) from a
    /// transport-class empty response or a parse-class failure (not cacheable).
    /// </summary>
    public async Task<CriteriaExtractionResult> ExtractFromDocumentAsync(
        string documentPath,
        string documentContent,
        string? component,
        CancellationToken ct = default)
    {
        // Spec 047: empty/whitespace source content is a genuine "nothing to
        // extract" result. Cacheable so we don't re-read an empty file on
        // every run.
        if (string.IsNullOrWhiteSpace(documentContent))
            return new CriteriaExtractionResult(ExtractionOutcome.Extracted, []);

        var service = await CopilotService.GetInstanceAsync(ct);
        await using var session = await service.CreateGenerationSessionAsync(_provider, ct: ct);

        var prompt = BuildExtractionPrompt(documentPath, documentContent, component);

        // Spec 040 follow-up: capture provider-reported usage or fall back.
        var observer = new CopilotUsageObserver();
        using var usageSub = session.On(evt =>
        {
            if (evt is AssistantUsageEvent u && u.Data is { } data)
                observer.RecordUsage(
                    (int)(data.InputTokens ?? 0),
                    (int)(data.OutputTokens ?? 0));
        });

        _onStatus?.Invoke($"Extracting criteria from {Path.GetFileName(documentPath)}...");

        var sw = Stopwatch.StartNew();
        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken: ct);
        sw.Stop();
        await observer.WaitForUsageAsync(TimeSpan.FromMilliseconds(200), ct);

        var responseText = response?.Data?.Content ?? "";
        var (tokensIn, tokensOut, estimated) = observer.GetOrEstimate(prompt, responseText);
        RecordAndLog(
            $"CRITERIA OK doc={Path.GetFileName(documentPath)} response_chars={responseText.Length} elapsed={sw.Elapsed.TotalSeconds:F1}s",
            sw.Elapsed, tokensIn, tokensOut, estimated);

        return ClassifyResponse(
            responseText,
            documentPath,
            component,
            ex => Spectra.CLI.Infrastructure.ErrorLogger.Write(
                "criteria", $"doc={documentPath}", ex));
    }

    /// <summary>
    /// Splits compound criteria text and normalizes to RFC 2119 (for import flow).
    /// </summary>
    public async Task<IReadOnlyList<AcceptanceCriterion>> SplitAndNormalizeAsync(
        string rawText,
        string? sourceKey,
        string? component,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return [];

        var service = await CopilotService.GetInstanceAsync(ct);
        await using var session = await service.CreateGenerationSessionAsync(_provider, ct: ct);

        var prompt = BuildSplitPrompt(rawText, sourceKey, component);

        var observer = new CopilotUsageObserver();
        using var usageSub = session.On(evt =>
        {
            if (evt is AssistantUsageEvent u && u.Data is { } data)
                observer.RecordUsage(
                    (int)(data.InputTokens ?? 0),
                    (int)(data.OutputTokens ?? 0));
        });

        var sw = Stopwatch.StartNew();
        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: ct);
        sw.Stop();
        await observer.WaitForUsageAsync(TimeSpan.FromMilliseconds(200), ct);

        var responseText = response?.Data?.Content ?? "";
        var (tokensIn, tokensOut, estimated) = observer.GetOrEstimate(prompt, responseText);
        RecordAndLog(
            $"CRITERIA SPLIT source={sourceKey ?? "?"} response_chars={responseText.Length} elapsed={sw.Elapsed.TotalSeconds:F1}s",
            sw.Elapsed, tokensIn, tokensOut, estimated);

        // Split/normalize is the import-flow path (not cached); we still
        // collapse non-Extracted outcomes to an empty list here because the
        // caller (`ai analyze --import-criteria`) doesn't use the cache gate.
        var result = ClassifyResponse(responseText, sourceKey, component);
        return result.Criteria;
    }

    /// <summary>
    /// Spec 054: thin delegate to the relocated, model-free
    /// <see cref="Spectra.CLI.Extraction.ExtractionPromptCompiler.Assemble"/> so the in-process
    /// extraction path and the <c>compile-extraction-prompt</c> CLI surface share a single source
    /// of extraction-prompt truth.
    /// </summary>
    internal static string BuildExtractionPrompt(string docPath, string content, string? component,
        PromptTemplateLoader? templateLoader = null)
        => Spectra.CLI.Extraction.ExtractionPromptCompiler.Assemble(docPath, content, component, templateLoader);

    private static string BuildSplitPrompt(string rawText, string? sourceKey, string? component)
    {
        return $$"""
            Split the following text into individual testable acceptance criteria. Each criterion should be a single, atomic, testable statement.

            For each criterion:
            - text: Rewrite using RFC 2119 language (MUST, SHOULD, MAY). Preserve original meaning.
            - rfc2119: The primary RFC 2119 keyword used
            - priority: "high" for MUST/SHALL, "medium" for SHOULD, "low" for MAY
            - tags: 1-3 relevant tags

            Respond ONLY with a JSON array. No markdown, no explanation, no code fences.
            {{(sourceKey is not null ? $"\nSource: {sourceKey}" : "")}}
            {{(component is not null ? $"\nComponent: {component}" : "")}}

            Text to split:
            {{rawText}}
            """;
    }

    /// <summary>
    /// Spec 069: thin delegate to the rescued, model-free
    /// <see cref="Spectra.CLI.Extraction.CriteriaResponseClassifier.Classify"/>. Retained as
    /// <c>internal</c> so the pre-069 <c>CriteriaExtractor</c> tests drive the classifier through the
    /// same entry point until this class is deleted (Spec 069 Phase E).
    /// </summary>
    internal static CriteriaExtractionResult ClassifyResponse(
        string? responseText,
        string? source,
        string? component,
        Action<Exception>? onException = null)
        => CriteriaResponseClassifier.Classify(responseText, source, component, onException);
}
