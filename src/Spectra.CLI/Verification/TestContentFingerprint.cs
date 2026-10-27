using System.Security.Cryptography;
using System.Text;
using Spectra.CLI.Generation;
using Spectra.Core.Models;

namespace Spectra.CLI.Verification;

/// <summary>
/// Spec 090 (FR-A2): a fingerprint of the test content the critic judges — the same JSON shape the update prompt shows
/// (<see cref="UpdatePromptCompiler.SerializeTest"/>). The grounding block is not part of it, so writing a verdict's
/// block never makes that verdict look stale.
/// </summary>
public static class TestContentFingerprint
{
    public static string Of(TestCase test)
    {
        ArgumentNullException.ThrowIfNull(test);
        var json = UpdatePromptCompiler.SerializeTest(test).Replace("\r\n", "\n");
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}
