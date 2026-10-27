using System.Text;
using System.Text.Json;

namespace Spectra.CLI.Tests.TestFixtures;

/// <summary>Spec 086: git helpers for a <see cref="BaselineTestWorkspace"/> whose root is a throwaway repository.</summary>
public static class GitWorkspace
{
    public static void InitGit(this BaselineTestWorkspace ws) => GitFixture.Init(ws.Root);

    public static string Commit(this BaselineTestWorkspace ws, string message, DateTimeOffset? date = null) =>
        GitFixture.Commit(ws.Root, message, date);

    /// <summary><c>git mv</c> inside the workspace; removes the source folder if git left it empty.</summary>
    public static void Move(this BaselineTestWorkspace ws, string from, string to)
    {
        GitFixture.Move(ws.Root, from, to);
        var folder = Path.GetDirectoryName(ws.PathOf(from))!;
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
        {
            Directory.Delete(folder);
        }
    }

    /// <summary>
    /// Spec 087: <c>git clone</c> of <paramref name="source"/>'s repository into <paramref name="target"/>'s root, without
    /// its submodules (their folders stay empty). The target root is emptied first. A fixture command.
    /// </summary>
    public static void CloneInto(this BaselineTestWorkspace source, BaselineTestWorkspace target)
    {
        GitFixture.DeleteTree(target.Root);
        GitFixture.Git(Path.GetDirectoryName(target.Root)!, "clone", "-q", source.Root, target.Root);
        GitFixture.AppendIdentityConfig(target.Root);
    }

    /// <summary>Checks out the submodules of <paramref name="ws"/> (not recursive). A fixture command.</summary>
    public static void InitSubmodules(this BaselineTestWorkspace ws) =>
        GitFixture.Git(ws.Root, "-c", "protocol.file.allow=always", "submodule", "update", "--init", "-q");

    public static void Edit(this BaselineTestWorkspace ws, string relative, string from, string to) =>
        File.WriteAllText(ws.PathOf(relative), File.ReadAllText(ws.PathOf(relative)).Replace(from, to));

    /// <summary>
    /// A doc large enough that a one-word edit stays above git's 90% rename similarity: the given sections, then a
    /// long background section.
    /// </summary>
    public static string Doc(string title, params (string Heading, string Body)[] sections)
    {
        var sb = new StringBuilder($"# {title}\n\n");
        foreach (var (heading, body) in sections)
        {
            sb.Append($"## {heading}\n\n{body}\n\n");
        }

        sb.Append("## Background\n\n");
        for (var i = 1; i <= 40; i++)
        {
            sb.Append($"Background line {i} about {title} and the many details the service documents.\n");
        }

        return sb.ToString();
    }

    public static string StorageDoc(string quota = "10GB") =>
        Doc("Storage", ("Quotas", $"Each user gets {quota} of storage space. Users must log in first."), ("Sharing", "Files can be shared by link."));

    /// <summary>The triage group of <paramref name="id"/> (asserts there is one).</summary>
    public static JsonElement Group(JsonElement triage, string id)
    {
        var group = BaselineTestWorkspace.GroupOf(triage, id);
        Assert.True(group.ValueKind == JsonValueKind.Object, $"{id} is in no group: {triage}");
        return group;
    }

    public static string[] Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToArray();

    /// <summary>The <c>removed</c> entry of <paramref name="id"/>'s ref (asserts there is one).</summary>
    public static JsonElement Removed(JsonElement triage, string id) =>
        Assert.Single(triage.GetProperty("removed").EnumerateArray(), r => r.GetProperty("test").GetString() == id);

    /// <summary>The detail file's record for <paramref name="id"/>.</summary>
    public static JsonElement DetailOf(this BaselineTestWorkspace ws, string id) =>
        BaselineTestWorkspace.Json(File.ReadAllText(ws.PathOf(".spectra/update-triage.json"))).GetProperty("tests")
            .EnumerateArray().Single(t => t.GetProperty("id").GetString() == id);

    /// <summary>The <c>to</c> of <paramref name="id"/>'s first relinked ref in a relink-sources result, or null.</summary>
    public static string? RelinkedTo(JsonElement relink, string id) =>
        relink.GetProperty("relinked").EnumerateArray()
            .Where(r => r.GetProperty("test").GetString() == id)
            .SelectMany(r => r.GetProperty("refs").EnumerateArray())
            .Select(r => r.GetProperty("to").GetString())
            .FirstOrDefault();
}
