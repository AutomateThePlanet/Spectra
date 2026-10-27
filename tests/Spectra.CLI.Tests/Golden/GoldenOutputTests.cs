using System.Security.Cryptography;
using System.Text;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Golden;

/// <summary>
/// Spec 099 (FR-001/FR-002, SC-001): 099 changes no output. A real-init workspace is driven through the commands 099
/// touches — help, init, docs index, source baseline, ingest (new tests, one edit, a staged batch with failures),
/// triage, update prompts, verdicts, grounding, audit, the generation plan, a batch prompt and the read-only commands —
/// and every exit code, stdout, stderr and the final file tree are compared with golden files captured on c24d23e.
/// </summary>
/// <remarks>
/// <para>Capture: set <c>SPECTRA_GOLDEN_RECORD</c> to <c>1</c> (writes <c>Golden/099/</c>) or to a directory, and run
/// this class on the build to capture. The test uses only fixtures that exist on c24d23e.</para>
/// <para>The golden files hold the platform's newlines (Windows), so the comparison runs on Windows only; the folder is
/// <c>-text</c> in <c>.gitattributes</c> so git keeps the bytes.</para>
/// <para>Spec 100 (research R8): 100 changes text that <c>init</c> writes (<c>CUSTOMIZATION.md</c>, the criteria file
/// comments), so the two runs no longer share one file tree. The fresh run compares with <c>files.txt</c> (re-captured on
/// 100); the pre-099 run — whose init-written files are the c24d23e inputs — compares with <c>files-pre099.txt</c>, a
/// byte copy of the 099 capture, which proves every command still writes byte-identical files on an existing workspace.
/// The record mode never rewrites <c>pre099-workspace/</c> or <c>files-pre099.txt</c>: both are frozen c24d23e
/// fixtures. Spec 102 re-captured <c>files.txt</c> once more for its one <c>CUSTOMIZATION.md</c> sentence (the critic
/// template is used; the critic model is <c>ai.critic.model</c>); every command output stayed byte-identical. Spec 103
/// re-captured it for one new <c>.gitignore</c> entry (<c>.spectra/reviews/</c>); every command output stayed
/// byte-identical.</para>
/// </remarks>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class GoldenOutputTests : IDisposable
{
    private const string RecordVariable = "SPECTRA_GOLDEN_RECORD";
    private const string Suite = "payments";
    private const string FilesRecord = "files.txt";
    private const string Pre099FilesRecord = "files-pre099.txt";
    private const string Pre099Workspace = "pre099-workspace";
    private const string InitStep = "05-init";

    private readonly BaselineTestWorkspace _ws = new(writeConfig: false);

    public void Dispose() => _ws.Dispose();

    [WindowsFact]
    public Task FreshWorkspace_MatchesTheGoldenOutput() => RunAsync(fromPre099: false);

    /// <summary>The 083 rule: the same sequence on the workspace c24d23e's init produced (captured with the golden files).</summary>
    [WindowsFact]
    public Task Pre099Workspace_MatchesTheGoldenOutput() => RunAsync(fromPre099: true);

    /// <summary>Spec 100 (C3): the frozen c24d23e workspace still holds 099's init output, so no re-capture replaced it.</summary>
    [Fact]
    public void Pre099Workspace_IsTheFrozenC24d23eCapture()
    {
        var customization = File.ReadAllText(Path.Combine(GoldenDirectory(), Pre099Workspace, "CUSTOMIZATION.md"));
        Assert.Contains("Re-run `spectra ai generate`", customization, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(GoldenDirectory(), Pre099FilesRecord)), "files-pre099.txt is missing");
    }

    private async Task RunAsync(bool fromPre099)
    {
        var record = Environment.GetEnvironmentVariable(RecordVariable);
        var recording = !string.IsNullOrEmpty(record) && !fromPre099;
        var actual = new SortedDictionary<string, string>(StringComparer.Ordinal);

        async Task Step(string name, params string[] args)
        {
            var (exit, stdout, stderr) = await _ws.RunAsync(args);
            actual[name + ".txt"] =
                $"# step: {name} spectra {string.Join(' ', args)}\n# exit: {exit}\n## stdout\n{stdout}\n## stderr\n{stderr}\n";
        }

        await Step("00-help", "--help");
        await Step("01-docs-help", "docs", "--help");
        await Step("02-ai-help", "ai", "--help");
        await Step("03-apply-move-help", "docs", "apply-move", "--help");
        await Step("04-retire-help", "docs", "retire", "--help");

        if (fromPre099)
        {
            CopyTree(Path.Combine(GoldenDirectory(), Pre099Workspace), _ws.Root);
        }
        else
        {
            await Step(InitStep, "init", "--skip-skills", "--output-format", "json");
        }

        WriteFixture();
        await Step("06-docs-index", "docs", "index", "--output-format", "json");
        await Step("07-source-baseline-init", "ai", "source-baseline", "--init", "--all-suites", "--output-format", "json");
        await Step("08-update-ack-before-triage", "ai", "update-ack", "--suite", Suite, "--group", "G1", "--output-format", "json");
        await Step("09-ingest-tests", "ai", "ingest-tests", Suite, "--from", ".spectra/golden/new-tests.json", "--output-format", "json");

        _ws.WriteDoc("docs/payments/checkout.md", CheckoutDocEdited);
        await Step("10-update-triage", "ai", "update-triage", "--suite", Suite, "--output-format", "json");
        await Step("11-update-ack-dry-run", "ai", "update-ack", "--suite", Suite, "--group", "G1", "--dry-run", "--output-format", "json");
        await Step("12-compile-update-prompt-g99", "ai", "compile-update-prompt", "--suite", Suite, "--group", "G99", "--output-format", "json");
        await Step("13-compile-update-prompt-g1", "ai", "compile-update-prompt", "--suite", Suite, "--group", "G1");
        await Step("14-compile-update-prompt-test", "ai", "compile-update-prompt", "--suite", Suite, "--test-id", "TC-101");
        await Step("15-ingest-update-test", "ai", "ingest-update", Suite, "--test-id", "TC-101", "--from", ".spectra/golden/edit-TC-101.json", "--output-format", "json");

        StageUpdates();
        await Step("16-ingest-update-all", "ai", "ingest-update", Suite, "--all", "--output-format", "json");

        _ws.WriteVerdict("TC-101", "grounded");
        _ws.WriteVerdict("TC-104", "partial");
        _ws.WriteVerdict("TC-105", "hallucinated");
        await Step("17-ingest-verdict-all", "ai", "ingest-verdict", "--suite", Suite, "--all", "--output-format", "json");
        await Step("18-ingest-grounding-all", "ai", "ingest-grounding", "--suite", Suite, "--all", "--ids", "TC-101", "--output-format", "json");
        await Step("19-audit-grounding", "ai", "audit-grounding", "--suite", Suite, "--output-format", "json");

        File.WriteAllText(_ws.PathOf(".spectra/golden/analysis.json"), GenerationPlanWorkspace.AnalysisJson(
            ("Large card payment needs confirmation", "docs/payments/checkout.md#cards"),
            ("Refund inside the window", "docs/payments/checkout.md#refund-window"),
            ("Partial refund capped at the paid amount", "docs/payments/refunds.md#partial-refunds"),
            ("Currency shown on the receipt", "docs/payments/receipts.md#currency")));
        await Step("20-ingest-analysis", "ai", "ingest-analysis", "--suite", Suite, "--from", ".spectra/golden/analysis.json", "--output-format", "json");
        await Step("21-compile-prompt-batch", "ai", "compile-prompt", "--suite", Suite, "--batch", "1", "--output-format", "json");

        await Step("22-validate", "validate", "--output-format", "json");
        await Step("23-show", "show", "TC-103", "--output-format", "json");
        await Step("24-docs-list-suites", "docs", "list-suites", "--output-format", "json");
        await Step("25-coverage", "ai", "analyze", "--coverage", "--format", "json", "--output-format", "json");
        await Step("26-dashboard-dry-run", "dashboard", "--output", "site", "--dry-run", "--output-format", "json");

        actual[FilesRecord] = FileTree(_ws.Root);

        var masked = actual.ToDictionary(kv => kv.Key, kv => GoldenMasks.Apply(kv.Value, _ws.Root), StringComparer.Ordinal);
        if (recording)
        {
            Write(RecordDirectory(record!), masked);
            return;
        }

        Compare(GoldenDirectory(), masked, skip: fromPre099 ? InitStep + ".txt" : null,
            filesRecord: fromPre099 ? Pre099FilesRecord : FilesRecord);
    }

    // ------------------------------------------------------------------ fixture

    private const string CheckoutDoc =
        "# Checkout {#checkout}\n\nHow checkout works.\n\n"
        + "## Card payments {#cards}\n\nCards are charged in **EUR**. A payment above 5000 EUR MUST be confirmed.\n\n"
        + "```bash\n# not a heading\necho charge\n```\n\n"
        + "## Limits\n\nFirst limits: 3 attempts.\n\n"
        + "## Limits\n\nSecond limits: 10 payments per day.\n\n"
        + "## Refund window\n\nRefunds are allowed for 30 days.\n";

    private const string CheckoutDocEdited =
        "# Checkout {#checkout}\n\nHow checkout works.\n\n"
        + "## Card payments {#cards}\n\nCards are charged in **EUR**. A payment above 3000 EUR MUST be confirmed.\n\n"
        + "```bash\n# not a heading\necho charge\n```\n\n"
        + "## Limits\n\nFirst limits: 3 attempts.\n\n"
        + "## Limits\n\nSecond limits: 10 payments per day.\n\n"
        + "## Refund window\n\nRefunds are allowed for thirty days.\n";

    private const string RefundsDoc =
        "# Refunds\n\n## Partial refunds\n\nA partial refund MUST NOT exceed the paid amount.\n";

    private const string ReceiptsDoc =
        "# Receipts\n\n## Currency\n\nThe receipt shows the currency of the payment.\n";

    private const string CheckoutCriteria =
        "criteria:\n"
        + "  - id: AC-PAY-001\n    text: \"A payment above 5000 EUR MUST be confirmed\"\n    rfc2119: MUST\n    source_type: document\n    source_doc: docs/payments/checkout.md\n    source_section: Card payments\n    component: payments\n"
        + "  - id: AC-PAY-002\n    text: \"Refunds MUST be allowed for 30 days\"\n    rfc2119: MUST\n    source_type: document\n    source_doc: docs/payments/checkout.md\n    source_section: Refund window\n    component: payments\n";

    private const string RefundsCriteria =
        "criteria:\n"
        + "  - id: AC-REF-001\n    text: \"A partial refund MUST NOT exceed the paid amount\"\n    rfc2119: MUST\n    source_type: document\n    source_doc: docs/payments/refunds.md\n    source_section: Partial refunds\n    component: payments\n";

    private const string ReceiptsCriteria =
        "criteria:\n"
        + "  - id: AC-RCP-001\n    text: \"The receipt MUST show the payment currency\"\n    rfc2119: MUST\n    source_type: document\n    source_doc: docs/payments/receipts.md\n    source_section: Currency\n    component: payments\n";

    private static string Test(string id, string title, string[] refs, string[] criteria, string extraFrontmatter = "", string body = "") =>
        "---\n"
        + $"id: {id}\n"
        + "priority: high\n"
        + extraFrontmatter
        + "source_refs:\n" + string.Concat(refs.Select(r => $"  - \"{r}\"\n"))
        + (criteria.Length == 0 ? "" : "criteria:\n" + string.Concat(criteria.Select(c => $"  - {c}\n")))
        + "---\n\n"
        + $"# {title}\n\n"
        + "## Steps\n\n1. Open checkout\n2. Pay\n\n"
        + "## Expected Result\n\nThe payment is handled as documented\n"
        + body;

    private void WriteFixture()
    {
        _ws.WriteDoc("docs/payments/checkout.md", CheckoutDoc);
        _ws.WriteDoc("docs/payments/refunds.md", RefundsDoc);
        _ws.WriteDoc("docs/payments/receipts.md", ReceiptsDoc);
        _ws.WriteCriteriaFile("docs/payments/checkout.criteria.yaml", CheckoutCriteria);
        _ws.WriteCriteriaFile("docs/payments/refunds.criteria.yaml", RefundsCriteria);
        _ws.WriteCriteriaFile("docs/payments/receipts.criteria.yaml", ReceiptsCriteria);

        var dir = _ws.PathOf($"test-cases/{Suite}");
        Directory.CreateDirectory(dir);
        void Write(string id, string text) => File.WriteAllText(Path.Combine(dir, id + ".md"), text);

        // An unknown key with a column-0 comment above it, and an unknown body section (078 lossless rows).
        Write("TC-101", Test("TC-101", "Large card payment is confirmed", ["docs/payments/checkout.md#cards"], ["AC-PAY-001"],
            extraFrontmatter: "# reviewed by QA\nowner: team-a\n",
            body: "\n## Notes\n\nKeep this section.\n"));
        // CRLF line endings; a ref into the duplicated "Limits" heading.
        Write("TC-102", Test("TC-102", "Payment attempts are limited", ["docs/payments/checkout.md#limits"], []).Replace("\n", "\r\n"));
        // A scalar depends_on.
        Write("TC-103", Test("TC-103", "Refund inside the window", ["docs/payments/checkout.md#refund-window"], ["AC-PAY-002"],
            extraFrontmatter: "depends_on: TC-101\n"));
        Write("TC-104", Test("TC-104", "Partial refund is capped", ["docs/payments/refunds.md#partial-refunds"], ["AC-REF-001"]));
        // A whole-file ref.
        Write("TC-105", Test("TC-105", "Checkout page shows the rules", ["docs/payments/checkout.md"], []));
        // Unparseable: no title.
        Write("TC-106", "---\nid: TC-106\npriority: high\n---\n\nNo title here.\n");
        _ws.RebuildIndex(Suite);

        Directory.CreateDirectory(_ws.PathOf(".spectra/golden"));
        WriteLf(_ws.PathOf(".spectra/golden/new-tests.json"), """
            [
              {"id": "TC-107", "title": "Receipt shows the currency", "priority": "medium", "steps": ["Pay", "Open the receipt"], "expected_result": "The currency is shown", "source_refs": ["docs/payments/receipts.md#currency"], "criteria": []},
              {"id": "TC-108", "title": "Second partial refund is capped", "priority": "low", "steps": ["Refund part", "Refund the rest"], "expected_result": "The total never exceeds the paid amount", "source_refs": ["docs/payments/refunds.md#partial-refunds"], "criteria": ["AC-REF-001"]}
            ]
            """);
        WriteLf(_ws.PathOf(".spectra/golden/edit-TC-101.json"), """
            [
              {"id": "TC-101", "title": "Large card payment is confirmed", "priority": "high", "steps": ["Open checkout", "Pay 3001 EUR"], "expected_result": "The payment asks for confirmation above 3000 EUR", "source_refs": ["docs/payments/checkout.md#cards"], "criteria": ["AC-PAY-001"]}
            ]
            """);
    }

    private void StageUpdates()
    {
        var dir = _ws.PathOf($".spectra/updates/{Suite}");
        Directory.CreateDirectory(dir);
        WriteLf(Path.Combine(dir, "updated-TC-103.json"), """
            [
              {"id": "TC-103", "title": "Refund inside the window", "priority": "high", "steps": ["Open checkout", "Ask for a refund on day 29"], "expected_result": "The refund is accepted within thirty days", "source_refs": ["docs/payments/checkout.md#refund-window"], "criteria": ["AC-PAY-002"]}
            ]
            """);
        WriteLf(Path.Combine(dir, "updated-TC-104.json"), """
            [
              {"id": "TC-104", "title": "Partial refund is capped", "priority": "high", "steps": ["Refund"], "expected_result": "Capped", "source_refs": ["docs/payments/refunds.md#partial-refunds"], "criteria": []},
              {"id": "TC-109", "title": "Another test", "priority": "high", "steps": ["Refund"], "expected_result": "Capped", "source_refs": [], "criteria": []}
            ]
            """);
        WriteLf(Path.Combine(dir, "updated-TC-105.json"), """
            [
              {"id": "TC-999", "title": "Checkout page shows the rules", "priority": "high", "steps": ["Open checkout"], "expected_result": "Rules shown", "source_refs": ["docs/payments/checkout.md"], "criteria": []}
            ]
            """);
    }

    /// <summary>
    /// Writes a raw-string fixture with LF line endings. A raw string literal takes its newlines from this source file,
    /// which git checks out with CRLF under core.autocrlf=true, so the golden inputs must not depend on it.
    /// </summary>
    private static void WriteLf(string path, string text) => File.WriteAllText(path, text.ReplaceLineEndings("\n"));

    // ------------------------------------------------------------------ records

    /// <summary>
    /// Every file under the workspace, sorted ordinally: text inline (bytes kept, CR included, masks applied), binary as
    /// sha256. A file whose text a mask changed shows <c>&lt;mask:size&gt;</c> instead of its byte count.
    /// </summary>
    private static string FileTree(string root)
    {
        var sb = new StringBuilder();
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                     .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            var bytes = File.ReadAllBytes(Path.Combine(root, file));
            if (Array.IndexOf(bytes, (byte)0) >= 0)
            {
                sb.Append("### ").Append(file).Append(" (").Append(bytes.Length).Append(" bytes)\n");
                sb.Append("sha256: ").Append(Convert.ToHexString(SHA256.HashData(bytes))).Append('\n');
                continue;
            }

            var text = Encoding.UTF8.GetString(bytes);
            var masked = GoldenMasks.IsRunStatusFile(file) ? "<mask:run-status-file>" : GoldenMasks.Apply(text, root);
            var size = string.Equals(text, masked, StringComparison.Ordinal) ? bytes.Length.ToString() : "<mask:size>";
            sb.Append("### ").Append(file).Append(" (").Append(size).Append(" bytes)\n");
            sb.Append(masked).Append("\n### end\n");
        }

        return sb.ToString();
    }

    private static void Write(string dir, IReadOnlyDictionary<string, string> records)
    {
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, "*.txt").Where(f => Path.GetFileName(f) != Pre099FilesRecord))
        {
            File.Delete(old);
        }

        foreach (var (name, text) in records)
        {
            File.WriteAllBytes(Path.Combine(dir, name), Encoding.UTF8.GetBytes(text));
        }
    }

    private static void Compare(string dir, IReadOnlyDictionary<string, string> actual, string? skip, string filesRecord)
    {
        // The file tree is compared with the record for this run (files.txt or files-pre099.txt), under its own name.
        var expectedNames = Directory.GetFiles(dir, "*.txt").Select(f => Path.GetFileName(f)!)
            .Where(n => n != skip && n != FilesRecord && n != Pre099FilesRecord)
            .Append(FilesRecord)
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        var actualNames = actual.Keys.Where(n => n != skip).ToList();
        Assert.Equal(expectedNames, actualNames);

        var failures = new List<string>();
        foreach (var name in expectedNames)
        {
            var source = name == FilesRecord ? filesRecord : name;
            var expected = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(dir, source)));
            if (!string.Equals(expected, actual[name], StringComparison.Ordinal))
            {
                failures.Add($"{name}:\n{FirstDifference(expected, actual[name])}");
            }
        }

        Assert.True(failures.Count == 0, "golden output differs:\n\n" + string.Join("\n\n", failures));
    }

    private static string FirstDifference(string expected, string actual)
    {
        var e = expected.Split('\n');
        var a = actual.Split('\n');
        for (var i = 0; i < Math.Max(e.Length, a.Length); i++)
        {
            var el = i < e.Length ? e[i] : "<missing>";
            var al = i < a.Length ? a[i] : "<missing>";
            if (!string.Equals(el, al, StringComparison.Ordinal))
            {
                return $"  line {i + 1}\n  - expected: {Visible(el)}\n  + actual:   {Visible(al)}";
            }
        }

        return "  (identical lines, different length)";
    }

    private static string Visible(string line) => line.Replace("\r", "\\r");

    private static void CopyTree(string from, string to)
    {
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string RecordDirectory(string record) => record == "1" ? GoldenDirectory() : record;

    private static string GoldenDirectory() => Path.Combine(RepoRoot(), "tests", "Spectra.CLI.Tests", "Golden", "099");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("not inside the repository");
    }
}

/// <summary>A fact that runs on Windows only (the golden files hold Windows newlines).</summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "golden files are captured on Windows";
        }
    }
}
