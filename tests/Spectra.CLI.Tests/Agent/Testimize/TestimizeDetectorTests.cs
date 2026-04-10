using Spectra.CLI.Agent.Testimize;

namespace Spectra.CLI.Tests.Agent.Testimize;

/// <summary>
/// Spec 038: TestimizeDetector must never throw, even when dotnet is not on
/// the PATH. The result is best-effort: any failure → false.
/// </summary>
public class TestimizeDetectorTests
{
    [Fact]
    public async Task IsInstalledAsync_DoesNotThrow()
    {
        // The result depends on the host machine. We only assert that the
        // method completes without throwing within a reasonable time.
        var result = await TestimizeDetector.IsInstalledAsync(CancellationToken.None);
        // result is whatever it is — bool, no exception
        _ = result;
    }
}
