namespace Spectra.CLI.Services;

/// <summary>
/// Per-AI-call helper that captures token usage from the Copilot SDK's
/// <c>AssistantUsageEvent</c> (Spec 040 follow-up). The SDK does not expose
/// usage on the <c>SendAndWaitAsync</c> return value — usage flows through
/// a separate event on the session-wide handler pipeline. Call sites:
///
/// 1. <c>new CopilotUsageObserver()</c> before sending.
/// 2. Inside <c>session.On(evt =&gt; ...)</c>, when <c>evt is AssistantUsageEvent</c>,
///    call <see cref="RecordUsage"/> with the provider-reported counts.
/// 3. After <c>await session.SendAndWaitAsync(...)</c> returns, call
///    <see cref="WaitForUsageAsync"/> with a short grace (e.g. 200ms) to
///    absorb any usage event that arrives after <c>SessionIdleEvent</c>.
///    The SDK makes no ordering guarantee between usage and idle, so we
///    defend with a TaskCompletionSource-backed wait.
/// 4. Call <see cref="GetOrEstimate"/> with the prompt and response strings.
///    Returns observed counts if usage arrived, otherwise falls back to
///    <see cref="TokenEstimator"/> and sets <c>Estimated=true</c>.
///
/// Thread safety: all mutation uses <see cref="Interlocked"/> and
/// <see cref="Volatile"/>. Safe for the SDK's background event dispatcher.
/// </summary>
public sealed class CopilotUsageObserver
{
    private int _promptTokens;
    private int _completionTokens;
    private int _receivedFlag; // 0 = no usage event yet, 1 = received
    private readonly TaskCompletionSource<bool> _receivedTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// True once at least one <see cref="RecordUsage"/> call has been made.
    /// </summary>
    public bool UsageReceived => Volatile.Read(ref _receivedFlag) != 0;

    /// <summary>
    /// Accumulated prompt (input) tokens. Only meaningful when
    /// <see cref="UsageReceived"/> is true.
    /// </summary>
    public int ObservedPromptTokens => Volatile.Read(ref _promptTokens);

    /// <summary>
    /// Accumulated completion (output) tokens. Only meaningful when
    /// <see cref="UsageReceived"/> is true.
    /// </summary>
    public int ObservedCompletionTokens => Volatile.Read(ref _completionTokens);

    /// <summary>
    /// Add provider-reported usage for the current turn. Safe to call from
    /// any thread. Multiple calls (e.g. sub-agents, tool loops, MCP
    /// sampling) sum via <see cref="Interlocked.Add(ref int, int)"/> so we
    /// capture full cost of the turn including sub-invocations.
    /// </summary>
    public void RecordUsage(int promptTokens, int completionTokens)
    {
        Interlocked.Add(ref _promptTokens, promptTokens);
        Interlocked.Add(ref _completionTokens, completionTokens);
        if (Interlocked.Exchange(ref _receivedFlag, 1) == 0)
        {
            _receivedTcs.TrySetResult(true);
        }
    }

    /// <summary>
    /// If usage has not yet arrived, wait up to <paramref name="grace"/>
    /// for it. Returns immediately (true) when usage is already observed.
    /// Never throws — timeout and cancellation both return false silently.
    ///
    /// Needed because the Copilot SDK delivers <c>AssistantUsageEvent</c>
    /// on the shared handler pipeline with no documented ordering relative
    /// to <c>SessionIdleEvent</c> (which is what ends <c>SendAndWaitAsync</c>).
    /// </summary>
    public async Task<bool> WaitForUsageAsync(TimeSpan grace, CancellationToken ct)
    {
        if (UsageReceived) return true;
        if (grace <= TimeSpan.Zero) return false;

        try
        {
            await _receivedTcs.Task.WaitAsync(grace, ct).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns provider-reported counts if usage arrived; otherwise returns
    /// the <see cref="TokenEstimator"/> fallback based on <paramref name="prompt"/>
    /// and <paramref name="responseText"/> length and sets
    /// <c>Estimated = true</c>.
    /// </summary>
    public (int PromptTokens, int CompletionTokens, bool Estimated) GetOrEstimate(
        string? prompt, string? responseText)
    {
        if (UsageReceived)
        {
            return (ObservedPromptTokens, ObservedCompletionTokens, false);
        }

        return (
            TokenEstimator.Estimate(prompt),
            TokenEstimator.Estimate(responseText),
            true);
    }
}
