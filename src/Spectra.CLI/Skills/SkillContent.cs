namespace Spectra.CLI.Skills;

/// <summary>
/// Provides bundled SKILL file contents, loaded from embedded .md resources.
/// </summary>
public static class SkillContent
{
    public static readonly Dictionary<string, string> All = SkillResourceLoader.GetAllSkills();

    public static string Generate => All["spectra-generate"];
    public static string Coverage => All["spectra-coverage"];
    public static string Dashboard => All["spectra-dashboard"];
    public static string Validate => All["spectra-validate"];
    public static string List => All["spectra-list"];
    public static string InitProfile => All["spectra-init-profile"];
    public static string Help => All["spectra-help"];
    public static string Criteria => All["spectra-criteria"];
}
