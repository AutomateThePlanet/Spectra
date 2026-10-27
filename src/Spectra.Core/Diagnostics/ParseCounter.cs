namespace Spectra.Core.Diagnostics;

/// <summary>
/// Spec 099: counts the expensive reads a command makes (test-file parses, layout captures, whole-document heading
/// scans, JSON array parses) so the performance fixes are proven by deterministic counts, not by timings.
/// </summary>
/// <remarks>
/// Counting happens only inside a scope opened with <see cref="Begin"/>. The scope is held in an
/// <see cref="AsyncLocal{T}"/>, so it flows into the awaited calls and child tasks of the code that opened it (including
/// <c>Parallel.ForEachAsync</c>), and tests running in parallel never see each other's counts. Increments go to the
/// innermost scope only. Without a scope (every production run) a count is a single null check.
/// </remarks>
internal static class ParseCounter
{
    private static readonly AsyncLocal<Scope?> Current = new();

    /// <summary>Opens a scope on the current async flow; dispose it to restore the enclosing one.</summary>
    public static Scope Begin()
    {
        var scope = new Scope(Current.Value);
        Current.Value = scope;
        return scope;
    }

    /// <summary>One <c>TestCaseParser.Parse</c> attempt (successful or not).</summary>
    public static void FullParse()
    {
        if (Current.Value is { } scope)
        {
            Interlocked.Increment(ref scope.FullParsesField);
        }
    }

    /// <summary>One capture of the 078 layout.</summary>
    public static void LayoutCapture()
    {
        if (Current.Value is { } scope)
        {
            Interlocked.Increment(ref scope.LayoutCapturesField);
        }
    }

    /// <summary>One scan of a whole document's headings.</summary>
    public static void DocumentHeadingScan()
    {
        if (Current.Value is { } scope)
        {
            Interlocked.Increment(ref scope.DocumentHeadingScansField);
        }
    }

    /// <summary>One JSON array parse of model output.</summary>
    public static void JsonParse()
    {
        if (Current.Value is { } scope)
        {
            Interlocked.Increment(ref scope.JsonParsesField);
        }
    }

    public sealed class Scope : IDisposable
    {
        private readonly Scope? _parent;
        private bool _disposed;

        internal int FullParsesField;
        internal int LayoutCapturesField;
        internal int DocumentHeadingScansField;
        internal int JsonParsesField;

        internal Scope(Scope? parent) => _parent = parent;

        public int FullParses => Volatile.Read(ref FullParsesField);

        public int LayoutCaptures => Volatile.Read(ref LayoutCapturesField);

        public int DocumentHeadingScans => Volatile.Read(ref DocumentHeadingScansField);

        public int JsonParses => Volatile.Read(ref JsonParsesField);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (ReferenceEquals(Current.Value, this))
            {
                Current.Value = _parent;
            }
        }
    }
}
