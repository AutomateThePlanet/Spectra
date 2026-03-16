namespace Spectra.CLI.Tests.Profile;

/// <summary>
/// Tests in this collection run sequentially and not in parallel with each other.
/// Used for tests that redirect Console output.
/// </summary>
[CollectionDefinition("ConsoleOutput", DisableParallelization = true)]
public class ConsoleOutputCollection
{
}
