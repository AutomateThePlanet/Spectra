using Spectra.MCP.Identity;

namespace Spectra.Execution.Tests.Identity;

/// <summary>
/// Regression: <see cref="UserIdentityResolver"/> must resolve a SINGLE, stable identity for the whole
/// process. Each <c>spectra run</c> handler call builds a fresh <c>RunServices</c> (hence a fresh
/// resolver) and the run is stored/looked-up by identity (<c>GetActiveRunByUserAsync</c>). Before the
/// process-wide cache, a slow/blocked <c>git config user.name</c> spawn under load could make one
/// resolver return the git name and another fall back to <c>Environment.UserName</c> — two identities
/// in one session — so <c>start</c> then <c>advance</c> saw "no active run" (intermittent failures in
/// the run-loop/parity tests under parallel load). Identity must not depend on per-call git timing.
/// </summary>
public class UserIdentityStabilityTests
{
    [Fact]
    public void SeparateInstances_ResolveSameIdentity()
    {
        var a = new UserIdentityResolver().GetCurrentUser();
        var b = new UserIdentityResolver().GetCurrentUser();

        Assert.False(string.IsNullOrWhiteSpace(a));
        Assert.Equal(a, b);
    }

    [Fact]
    public void RepeatedResolution_IsStable_AcrossManyInstances()
    {
        // Mimics many short-lived handler processes resolving identity concurrently-ish in one process.
        var first = new UserIdentityResolver().GetCurrentUser();
        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(first, new UserIdentityResolver().GetCurrentUser());
        }
    }
}
