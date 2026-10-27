namespace Spectra.CLI.Skills;

/// <summary>
/// Spec 101 F2: the bundled reference files — material a skill reads only when its SKILL.md names it — keyed by the SKILL
/// name they belong to. Installed as <c>.claude/skills/&lt;name&gt;/reference.md</c> (<see cref="SkillInstallLayout.ReferencePath"/>).
/// </summary>
public static class SkillReferenceContent
{
    public static readonly Dictionary<string, string> All = SkillResourceLoader.GetAllReferences();

    public static string Generate => All["spectra-generate"];
    public static string Update => All["spectra-update"];
}
