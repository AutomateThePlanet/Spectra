namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for test execution settings.
/// </summary>
/// <remarks>
/// Spec 057 removed the <c>copilot_space</c> / <c>copilot_space_owner</c> fields (dead once the
/// execution agent's Copilot Spaces doc-lookup was replaced by native file reads). Legacy config files
/// that still carry those keys deserialize unchanged — unknown JSON properties are ignored. The type is
/// retained as the bind target for the <c>execution</c> config block.
/// </remarks>
public sealed class ExecutionConfig
{
}
