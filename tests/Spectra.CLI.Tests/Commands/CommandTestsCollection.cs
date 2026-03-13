namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Collection definition to disable parallel execution for command tests
/// that modify the current directory.
/// </summary>
[CollectionDefinition("Sequential Command Tests", DisableParallelization = true)]
public class CommandTestsCollection
{
}
