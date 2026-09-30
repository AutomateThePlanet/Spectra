namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 079: criteria command tests change the working directory and capture the global console, so they must not
/// run in parallel with any other test (another test's Console output would leak into the captured stdout).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class CriteriaCommandsCollection
{
    public const string Name = "CriteriaCommands";
}
