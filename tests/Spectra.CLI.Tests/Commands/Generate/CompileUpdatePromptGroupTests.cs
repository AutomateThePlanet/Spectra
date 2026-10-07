using System.Text.Json.Nodes;
using Spectra.CLI.Baseline;
using Spectra.CLI.Generation;
using Spectra.CLI.Services;
using Spectra.CLI.Tests.TestFixtures;
using Spectra.Core.Models.Config;
using static Spectra.CLI.Tests.TestFixtures.UpdatePromptWorkspace;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 088 T3, T6, T7 and SC-001 (FR-007–FR-010, FR-013): one prompt per triage group — shared sections and criteria
/// once, every test once, deterministic, validated like <c>update-ack --group</c>, and within the token budget.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CompileUpdatePromptGroupTests : IDisposable
{
    private const string Storage =
        "# Storage\n\n## Quotas\n\nEach user gets **30GB** of storage space. Users must log in first.\n\n"
        + "## Sharing\n\nFiles can be shared by link.\n";

    private readonly BaselineTestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private void Edit(string from, string to) =>
        _ws.WriteDoc("docs/storage.md", File.ReadAllText(_ws.PathOf("docs/storage.md")).Replace(from, to));

    /// <summary>TC-101 [Quotas] + AC-1, TC-102 [Sharing, Quotas] + AC-1, TC-103 [Quotas]; 30GB → 45GB; triaged.</summary>
    private async Task<string> ThreeTestGroupAsync()
    {
        _ws.WriteDoc("docs/storage.md", Storage);
        _ws.WriteCriteria("storage.criteria.yaml", ("AC-1", "Each user MUST get 30GB"), ("AC-2", "Files MAY be shared by link"));
        _ws.WriteTest("storage", "TC-101", ["docs/storage.md#Quotas"], ["AC-1"]);
        _ws.WriteTest("storage", "TC-102", ["docs/storage.md#Sharing", "docs/storage.md#Quotas"], ["AC-1"]);
        _ws.WriteTest("storage", "TC-103", ["docs/storage.md#Quotas"]);
        await _ws.InitBaselineAsync();
        Edit("30GB", "45GB");
        var triage = await _ws.TriageAsync();
        var group = Assert.Single(triage.GetProperty("groups").EnumerateArray());
        Assert.Equal(["TC-101", "TC-102", "TC-103"], GitWorkspace.Strings(group.GetProperty("tests")));
        return group.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task SharedSectionsAndCriteria_AppearOnce_EveryTestOnce_InOrder()
    {
        var group = await ThreeTestGroupAsync();

        var (exit, prompt, error) = await _ws.CompileAsync("--suite", "storage", "--group", group);

        Assert.True(exit == 0, error);
        Assert.Equal(1, Count(prompt, "— docs/storage.md#Quotas  (tests: TC-101, TC-102, TC-103)"));
        Assert.Equal(1, Count(prompt, "#### S1 — docs/storage.md#Quotas"));
        Assert.Equal(1, Count(prompt, "#### S2 — docs/storage.md#Sharing"));
        Assert.Equal(1, Count(prompt, "- AC-1: Each user MUST get 30GB"));
        Assert.DoesNotContain("AC-2", prompt);
        Assert.Equal(1, Count(prompt, "## OUTPUT FORMAT"));
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103" })
        {
            Assert.Equal(1, Count(prompt, $"### TEST {id}\n"));
        }

        Assert.True(prompt.IndexOf("### TEST TC-101", StringComparison.Ordinal) < prompt.IndexOf("### TEST TC-102", StringComparison.Ordinal));
        Assert.True(prompt.IndexOf("### TEST TC-102", StringComparison.Ordinal) < prompt.IndexOf("### TEST TC-103", StringComparison.Ordinal));
        Assert.Contains("### TEST TC-102\nSections: S2, S1\nCriteria: AC-1", prompt);
    }

    [Fact]
    public async Task NoTriage_IsTriageMissing()
    {
        var group = await ThreeTestGroupAsync();
        File.Delete(_ws.PathOf(".spectra/update-triage.json"));

        var (exit, _, error) = await _ws.CompileJsonAsync("--suite", "storage", "--group", group);

        Assert.Equal(4, exit);
        Assert.Equal("TRIAGE_MISSING", Failure(error).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task UnknownGroup_IsGroupNotFound()
    {
        await ThreeTestGroupAsync();

        var (exit, _, error) = await _ws.CompileJsonAsync("--suite", "storage", "--group", "G9");

        Assert.Equal(4, exit);
        Assert.Equal("GROUP_NOT_FOUND", Failure(error).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task AChangeAfterTriage_IsTriageStale()
    {
        var group = await ThreeTestGroupAsync();
        Edit("45GB", "60GB");

        var (exit, _, error) = await _ws.CompileJsonAsync("--suite", "storage", "--group", group);

        Assert.Equal(4, exit);
        Assert.Equal("TRIAGE_STALE", Failure(error).GetProperty("error_code").GetString());
    }

    [Fact]
    public async Task SameWorkspace_GivesByteIdenticalPrompts()
    {
        var group = await ThreeTestGroupAsync();

        var (_, a, _) = await _ws.CompileAsync("--suite", "storage", "--group", group);
        var (_, b, _) = await _ws.CompileAsync("--suite", "storage", "--group", group);
        var (_, c, _) = await _ws.CompileAsync("--suite", "storage", "--test-id", "TC-102");
        var (_, d, _) = await _ws.CompileAsync("--suite", "storage", "--test-id", "TC-102");

        Assert.Equal(a, b);
        Assert.Equal(c, d);
    }

    [Fact]
    public async Task OverTheBudget_IsPromptTooLarge_WhileEachTestFits()
    {
        var group = await ThreeTestGroupAsync();
        var single = new List<int>();
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103" })
        {
            single.Add(TokenEstimator.Estimate((await _ws.CompileAsync("--suite", "storage", "--test-id", id)).Out));
        }

        var whole = TokenEstimator.Estimate((await _ws.CompileAsync("--suite", "storage", "--group", group)).Out);
        var limit = (single.Max() + whole) / 2;
        Assert.InRange(limit, single.Max() + 1, whole - 1);
        var config = JsonNode.Parse(File.ReadAllText(_ws.PathOf("spectra.config.json")))!;
        config["analysis"] = new JsonObject { ["max_prompt_tokens"] = limit };
        File.WriteAllText(_ws.PathOf("spectra.config.json"), config.ToJsonString());

        var (exit, output, error) = await _ws.CompileJsonAsync("--suite", "storage", "--group", group);

        Assert.Equal(4, exit);
        Assert.Empty(output);
        var failure = Failure(error);
        Assert.Equal("PROMPT_TOO_LARGE", failure.GetProperty("error_code").GetString());
        Assert.Contains($"{limit}-token budget", failure.GetProperty("errors")[0].GetString());
        foreach (var id in new[] { "TC-101", "TC-102", "TC-103" })
        {
            Assert.Equal(0, (await _ws.CompileAsync("--suite", "storage", "--test-id", id)).Exit);
        }
    }

    [Fact]
    public async Task SC001_TheVariablePart_IsAtMost1Point3TimesTheRawParts()
    {
        // A realistic group: one changed section of about 15 lines; 10 tests with 5 steps each, each linking its own criterion.
        var section = "## Quotas\n\n" + string.Concat(Enumerable.Range(1, 14).Select(i =>
            $"Rule {i}: accounts on the standard plan get {(i == 1 ? "30GB" : $"{i} units")} for item {i}, checked at sign-in.\n"));
        _ws.WriteDoc("docs/storage.md", "# Storage\n\n" + section + "\n## Sharing\n\nFiles can be shared by link.\n");
        var criteria = Enumerable.Range(1, 10)
            .Select(i => ($"AC-{i:D2}", $"Users on the standard plan MUST see rule {i} applied. The limit is shown before upload."))
            .ToArray();
        _ws.WriteCriteria("storage.criteria.yaml", criteria);
        for (var i = 1; i <= 10; i++)
        {
            var id = $"TC-{100 + i}";
            var markdown = BaselineTestWorkspace.TestMarkdown(id, ["docs/storage.md#Quotas"], [$"AC-{i:D2}"], $"Quota rule {i}")
                .Replace("## Steps\n\n1. Do the thing\n",
                    "## Steps\n\n" + string.Concat(Enumerable.Range(1, 5).Select(s => $"{s}. Step {s} of checking quota rule {i} for a standard account\n")))
                .Replace("It works", $"The quota for rule {i} matches the documented limit and the upload is accepted");
            Directory.CreateDirectory(_ws.PathOf("test-cases/storage"));
            File.WriteAllText(_ws.PathOf($"test-cases/storage/{id}.md"), markdown);
        }

        _ws.RebuildIndex("storage");
        await _ws.InitBaselineAsync();
        _ws.WriteDoc("docs/storage.md", File.ReadAllText(_ws.PathOf("docs/storage.md")).Replace("30GB", "45GB"));
        var group = Assert.Single((await _ws.TriageAsync()).GetProperty("groups").EnumerateArray());
        Assert.Equal(10, group.GetProperty("tests").GetArrayLength());

        var testsPath = _ws.PathOf("test-cases");
        var scan = await BaselineStore.ScanSuiteAsync(_ws.Root, testsPath, "storage", CancellationToken.None);
        var context = await BaselineContext.CreateAsync(_ws.Root, SpectraConfig.Default, CancellationToken.None);
        var baseline = await BaselineStore.LoadSuiteAsync(_ws.Root, testsPath, "storage", CancellationToken.None);
        var ctx = await UpdateContextBuilder.BuildAsync(scan.Tests, baseline, context, 12, CancellationToken.None);

        var variable = UpdatePromptRenderer.SourceDiff(ctx).Length + UpdatePromptRenderer.Criteria(ctx).Length
            + UpdatePromptRenderer.Tests(ctx).Length;
        var s1 = Assert.Single(ctx.Sections);
        var whatChanged = string.Join("; ", s1.Diff.Select(d => $"{(d.Added ? "added" : "removed")} \"{string.Join(" ", d.Words)}\""));
        var raw = s1.Current!.Length + s1.Previous!.Length + whatChanged.Length
            + ctx.Criteria.Sum(c => c.Text!.Length)
            + ctx.Tests.Sum(t => UpdatePromptCompiler.SerializeTest(t.Test).Length);

        Assert.True(variable <= 1.3 * raw, $"variable part {variable} chars vs raw parts {raw} chars ({(double)variable / raw:0.00}×)");
        var (exit, prompt, error) = await _ws.CompileAsync("--suite", "storage", "--group", group.GetProperty("id").GetString()!);
        Assert.True(exit == 0, error);
        Assert.Contains(UpdatePromptRenderer.Tests(ctx).TrimEnd('\n'), prompt);
    }
}
