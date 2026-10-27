namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 087: tests that set <c>GIT_*</c> variables in the test process. Not run in parallel with anything else, because
/// every git process the test run starts — fixtures included — would inherit them.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GitEnvironmentCollection
{
    public const string Name = "GitEnvironment";
}

/// <summary>Sets process environment variables and restores the previous values (including unset) on dispose.</summary>
public sealed class ScopedEnvironment : IDisposable
{
    private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

    public ScopedEnvironment(IReadOnlyDictionary<string, string?> variables)
    {
        foreach (var (name, value) in variables)
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public void Dispose()
    {
        foreach (var (name, value) in _previous)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
