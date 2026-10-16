using System.Xml.Linq;

namespace Spectra.CLI.Tests.Infrastructure;

/// <summary>
/// Spec 099 F6 (FR-018): the CLI and Core projects treat warnings as errors in every configuration, so a new warning
/// fails the build. NuGet vulnerability advisories (NU1901–NU1904) stay warnings: one published after the fact must not
/// break a build that changed nothing. On c24d23e the setting existed only for Release, so 4 warnings went unnoticed.
/// </summary>
public sealed class WarningsAsErrorsGuardTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("not inside the repository");
    }

    [Theory]
    [InlineData("src/Spectra.CLI/Spectra.CLI.csproj")]
    [InlineData("src/Spectra.Core/Spectra.Core.csproj")]
    public void Project_TreatsWarningsAsErrors_InEveryConfiguration(string project)
    {
        var xml = XDocument.Load(Path.Combine(RepoRoot(), project));

        var setting = xml.Descendants("TreatWarningsAsErrors").SingleOrDefault();
        Assert.NotNull(setting);
        Assert.Equal("true", setting!.Value.Trim(), ignoreCase: true);
        Assert.Null(setting.Attribute("Condition"));
        Assert.Null(setting.Parent!.Attribute("Condition"));

        var notAsErrors = xml.Descendants("WarningsNotAsErrors").SingleOrDefault()?.Value ?? "";
        foreach (var code in new[] { "NU1901", "NU1902", "NU1903", "NU1904" })
        {
            Assert.Contains(code, notAsErrors.Split(';', StringSplitOptions.TrimEntries));
        }
    }
}
