using System.Text.Json;
using Spectra.Core.Models.Config;
using Xunit;

namespace Spectra.Core.Tests.Config;

/// <summary>
/// Spec 057 (FR-003 / SC-004) — the dead <c>copilot_space</c> / <c>copilot_space_owner</c> fields were
/// removed from <see cref="ExecutionConfig"/>. A legacy config that still carries those keys must
/// continue to deserialize (unknown JSON properties are ignored) — no migration, no shim.
/// </summary>
public class ExecutionConfigBackCompatTests
{
    [Fact]
    public void LegacyExecutionBlock_WithCopilotSpaceKeys_StillDeserializes()
    {
        const string json = """
            { "copilot_space": "my-space", "copilot_space_owner": "acme" }
            """;

        var config = JsonSerializer.Deserialize<ExecutionConfig>(json);

        Assert.NotNull(config); // unknown keys ignored — no exception, valid object
    }
}
