using Spectra.Core.Git;

namespace Spectra.Core.Tests.Git;

/// <summary>
/// Spec 087 T3 (F3, FR-009–FR-011): the environment of every git process SPECTRA starts drops the variables that
/// redirect git to another repository, index, object store or configuration, and keeps optional locks and prompts off.
/// </summary>
public class GitEnvironmentTests
{
    public static IEnumerable<object[]> Redirecting() =>
        new[]
        {
            "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES",
            "GIT_COMMON_DIR", "GIT_NAMESPACE", "GIT_CEILING_DIRECTORIES", "GIT_DISCOVERY_ACROSS_FILESYSTEM", "GIT_CONFIG",
            "GIT_CONFIG_PARAMETERS", "GIT_CONFIG_COUNT", "GIT_CONFIG_KEY_0", "GIT_CONFIG_VALUE_0", "GIT_CONFIG_KEY_12"
        }.Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(Redirecting))]
    public void Apply_RemovesEveryRedirectingVariable(string name)
    {
        var env = new Dictionary<string, string?> { [name] = "x", ["PATH"] = "p" };

        GitEnvironment.Apply(env);

        Assert.False(env.ContainsKey(name));
        Assert.Equal("p", env["PATH"]);
    }

    [Fact]
    public void Apply_KeepsEverythingElse_AndSetsTheTwoSettings()
    {
        var env = new Dictionary<string, string?>
        {
            ["PATH"] = "p", ["HOME"] = "h", ["GIT_AUTHOR_NAME"] = "a", ["GIT_SSH_COMMAND"] = "ssh",
            ["GIT_OPTIONAL_LOCKS"] = "1", ["GIT_TERMINAL_PROMPT"] = "1"
        };

        GitEnvironment.Apply(env);

        Assert.Equal("p", env["PATH"]);
        Assert.Equal("h", env["HOME"]);
        Assert.Equal("a", env["GIT_AUTHOR_NAME"]);
        Assert.Equal("ssh", env["GIT_SSH_COMMAND"]);
        Assert.Equal("0", env["GIT_OPTIONAL_LOCKS"]);
        Assert.Equal("0", env["GIT_TERMINAL_PROMPT"]);
    }

    [Fact]
    public void Apply_SetsExtraLast()
    {
        var env = new Dictionary<string, string?>();

        GitEnvironment.Apply(env, new Dictionary<string, string> { ["GIT_TERMINAL_PROMPT"] = "0", ["LC_ALL"] = "C" });

        Assert.Equal("C", env["LC_ALL"]);
        Assert.Equal("0", env["GIT_OPTIONAL_LOCKS"]);
    }

    [Theory]
    [InlineData("git_dir", true, true)]
    [InlineData("git_dir", false, false)]
    [InlineData("Git_Config_Key_3", true, true)]
    [InlineData("GIT_DIR", false, true)]
    [InlineData("GIT_AUTHOR_NAME", true, false)]
    [InlineData("GIT_CONFIG_GLOBAL", true, false)]
    public void IsRedirecting(string name, bool ignoreCase, bool expected) =>
        Assert.Equal(expected, GitEnvironment.IsRedirecting(name, ignoreCase));

    [Fact]
    public void IsRedirecting_ByDefault_IgnoresCaseOnWindowsOnly() =>
        // Analysis C1: environment variable names are case-insensitive on Windows only.
        Assert.Equal(OperatingSystem.IsWindows(), GitEnvironment.IsRedirecting("git_dir"));

    [Fact]
    public void Settings_AreTheTwoFixedValues() =>
        Assert.Equal(
            new Dictionary<string, string> { ["GIT_OPTIONAL_LOCKS"] = "0", ["GIT_TERMINAL_PROMPT"] = "0" },
            GitEnvironment.Settings);
}
