using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>
/// Spec 099 F7: the real <c>spectra init</c> (with and without skills) runs once per test process into a template
/// folder; a test workspace still in its initial state gets a copy of it instead of running init again. A workspace that
/// already holds a file or an extra folder (a pre-written config, …) runs the real init, so init's own handling of an
/// existing workspace is never replaced. <see cref="TemplateWorkspaceTests"/> proves a copy equals a fresh init.
/// </summary>
public static class TemplateWorkspaces
{
    private static readonly Lazy<Task<string>> WithoutSkills = new(() => BuildAsync(skipSkills: true));
    private static readonly Lazy<Task<string>> WithSkills = new(() => BuildAsync(skipSkills: false));

    /// <summary>The folders a new <see cref="BaselineTestWorkspace"/> (without config) starts with.</summary>
    private static readonly string[] InitialFolders = [".spectra", "docs", "docs/criteria", "test-cases"];

    /// <summary>The result of the real init in <paramref name="ws"/>: a copy of the template, or the real init.</summary>
    public static async Task InitAsync(BaselineTestWorkspace ws, bool skipSkills)
    {
        if (!IsInitialState(ws.Root))
        {
            await RealInitAsync(ws.Root, skipSkills);
            return;
        }

        CopyTree(await (skipSkills ? WithoutSkills : WithSkills).Value, ws.Root);
    }

    /// <summary>The real <c>spectra init</c> (quiet, as the fixtures always ran it).</summary>
    public static async Task RealInitAsync(string root, bool skipSkills)
    {
        var logger = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet).CreateLogger<InitHandler>();
        var exit = await new InitHandler(logger, root).HandleAsync(force: false, skipSkills: skipSkills);
        if (exit != ExitCodes.Success)
        {
            throw new Xunit.Sdk.XunitException($"spectra init exited {exit}");
        }
    }

    /// <summary>No file, and exactly the folders a new workspace without config has.</summary>
    public static bool IsInitialState(string root)
    {
        if (Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any())
        {
            return false;
        }

        var folders = Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
            .Select(d => Path.GetRelativePath(root, d).Replace('\\', '/'))
            .OrderBy(d => d, StringComparer.Ordinal);
        return folders.SequenceEqual(InitialFolders.OrderBy(d => d, StringComparer.Ordinal));
    }

    private static async Task<string> BuildAsync(bool skipSkills)
    {
        // Built in a workspace of its own, in the same initial state as the workspaces that copy it. Kept for the
        // test process; removed when it exits.
        var template = new BaselineTestWorkspace(writeConfig: false);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => template.Dispose();
        await RealInitAsync(template.Root, skipSkills);
        return template.Root;
    }

    private static void CopyTree(string from, string to)
    {
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        }

        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), overwrite: false);
        }
    }
}
