using System.Security.Cryptography;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 099 F7: a workspace initialised from the shared template is byte-identical (every path, every byte) to one
/// where the real <c>spectra init</c> ran — so a template that embeds its own location, or init output that depends on
/// the run, fails here instead of silently changing what the tests see.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class TemplateWorkspaceTests : IDisposable
{
    private readonly List<BaselineTestWorkspace> _workspaces = [];

    public void Dispose()
    {
        foreach (var ws in _workspaces)
        {
            ws.Dispose();
        }
    }

    private BaselineTestWorkspace New()
    {
        var ws = new BaselineTestWorkspace(writeConfig: false);
        _workspaces.Add(ws);
        return ws;
    }

    private static Dictionary<string, string> Tree(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                f => Path.GetRelativePath(root, f).Replace('\\', '/'),
                f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));

    private static string[] Folders(string root) =>
        Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
            .Select(d => Path.GetRelativePath(root, d).Replace('\\', '/'))
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToArray();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ACopiedTemplate_EqualsAFreshInit(bool skipSkills)
    {
        var copied = New();
        var fresh = New();
        Assert.True(TemplateWorkspaces.IsInitialState(copied.Root));

        await TemplateWorkspaces.InitAsync(copied, skipSkills);
        await TemplateWorkspaces.RealInitAsync(fresh.Root, skipSkills);

        Assert.Equal(Tree(fresh.Root), Tree(copied.Root));
        Assert.Equal(Folders(fresh.Root), Folders(copied.Root));
    }

    [Fact]
    public async Task AWorkspaceThatIsNotInItsInitialState_RunsTheRealInit()
    {
        var withConfig = new BaselineTestWorkspace(); // writes spectra.config.json
        _workspaces.Add(withConfig);
        var config = File.ReadAllText(withConfig.PathOf("spectra.config.json"));

        Assert.False(TemplateWorkspaces.IsInitialState(withConfig.Root));

        // The real init refuses an initialised workspace (exit 1, no --force) and leaves its config alone; a template
        // copy would have replaced the config and succeeded.
        var refused = await Assert.ThrowsAsync<Xunit.Sdk.XunitException>(() => TemplateWorkspaces.InitAsync(withConfig, skipSkills: true));
        Assert.Contains("exited 1", refused.Message);
        Assert.Equal(config, File.ReadAllText(withConfig.PathOf("spectra.config.json")));
    }
}
