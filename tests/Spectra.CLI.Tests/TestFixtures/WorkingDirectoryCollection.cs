namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 096 F8: tests in the "WorkingDirectory" collection change the process working directory and Console, so they
/// must not run in parallel with any other collection. Before 096 the collection had no definition and ran in parallel
/// (flaky in 093).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class WorkingDirectoryCollection
{
    public const string Name = "WorkingDirectory";
}
