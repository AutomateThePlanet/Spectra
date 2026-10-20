using System.Text.Json;
using Spectra.CLI.Tests.TestFixtures;

namespace Spectra.CLI.Tests.Commands.Generate;

/// <summary>
/// Spec 102 F1 (FR-001, FR-002, FR-005; US1; research R2; contracts C1): every critic prompt's system part comes from
/// the <c>critic-verification</c> template — the workspace copy in <c>.spectra/prompts/</c>, else the bundled one — in
/// every form (suite, all tests, <c>--docs</c>, legacy <c>--test &lt;path&gt;</c>) and doc mode (cited sections,
/// <c>--full-docs</c>). The <c>--full-docs</c> user part stays byte-identical to the pre-101 golden. On 26bfe88 the
/// command passed no template loader, so the critic got a hard-coded prompt without the 037 / 075 sections and a
/// customised template was ignored.
/// </summary>
[Collection(CriteriaCommandsCollection.Name)]
public sealed class CriticTemplatePromptTests : IDisposable
{
    private readonly BaselineTestWorkspace _ws = new();

    public CriticTemplatePromptTests() => _ws.GoldenWorkspace();

    public void Dispose() => _ws.Dispose();

    private async Task<string> PromptAsync(params string[] args)
    {
        var (exit, output, error) = await _ws.RunAsync(["ai", "compile-critic-prompt", .. args]);
        Assert.True(exit == 0, $"exit {exit}: {error}");
        return CriticSourceWorkspace.Lf(output);
    }

    private static string SystemPart(string prompt)
    {
        var at = prompt.IndexOf(CriticSourceWorkspace.UserPartMarker, StringComparison.Ordinal);
        Assert.True(at >= 0, "the prompt has no user part");
        return prompt[..at];
    }

    [Theory]
    [InlineData("--suite s --test TC-001")]
    [InlineData("--suite s --test TC-001 --full-docs")]
    [InlineData("--suite s")]
    [InlineData("--suite s --test TC-001 --docs docs/short.md")]
    [InlineData("--test test-cases/s/TC-001.md --docs docs/short.md")]
    public async Task EveryFormAndMode_UsesTheTemplate(string args)
    {
        var system = SystemPart(await PromptAsync(args.Split(' ')));

        Assert.Contains("## Technique Verification", system);
        Assert.Contains("## Arithmetic Verification", system);
    }

    [Theory]
    [InlineData("--suite s --test TC-001")]
    [InlineData("--test test-cases/s/TC-001.md --docs docs/short.md")]
    public async Task AnEditedWorkspaceTemplate_IsUsed(string args)
    {
        _ws.WriteCriticTemplate("You are the CUSTOM critic. UNIQUECRITICMARKER\n\nReturn JSON only.");

        var prompt = await PromptAsync(args.Split(' '));

        Assert.Contains("UNIQUECRITICMARKER", prompt);
        Assert.DoesNotContain("Technique Verification", prompt);
    }

    [Fact]
    public async Task TheTemplatesPlaceholders_ResolveToEmpty()
    {
        _ws.WriteCriticTemplate("Custom critic. [{{acceptance_criteria}}] [{{test_case}}]\n\nReturn JSON only.");

        var prompt = await PromptAsync("--suite", "s", "--test", "TC-001");

        Assert.Contains("Custom critic. [] []", prompt);
        Assert.DoesNotContain("{{acceptance_criteria}}", prompt);
        Assert.DoesNotContain("{{test_case}}", prompt);
    }

    [Fact]
    public async Task ABrokenWorkspaceTemplate_FallsBackToTheBundledOne()
    {
        _ws.WriteDoc(".spectra/prompts/critic-verification.md", "no frontmatter here\n");

        var system = SystemPart(await PromptAsync("--suite", "s", "--test", "TC-001"));

        Assert.Contains("## Technique Verification", system);
    }

    [Fact]
    public async Task JsonMode_PrintsOnlyTheJsonDocument_WithTheTemplate()
    {
        var (exit, output, error) = await _ws.RunAsync(
            "ai", "compile-critic-prompt", "--suite", "s", "--test", "TC-001", "--output-format", "json");

        Assert.True(exit == 0, $"exit {exit}: {error}");
        using var doc = JsonDocument.Parse(output);
        var prompt = doc.RootElement.GetProperty("prompts")[0].GetProperty("prompt").GetString()!;
        Assert.Contains("## Arithmetic Verification", prompt);
    }

    [Fact]
    public async Task FullDocs_UserPart_IsByteIdenticalToThePre101Golden()
    {
        var prompt = await PromptAsync("--suite", "s", "--test", "TC-001", "--full-docs");

        Assert.Equal(CriticSourceWorkspace.UserPart(CriticSourceWorkspace.Golden()), CriticSourceWorkspace.UserPart(prompt));
    }

    [Fact]
    public async Task FullDocs_IsByteIdenticalToThe102Golden()
    {
        Assert.Equal(CriticSourceWorkspace.Golden102(), await PromptAsync("--suite", "s", "--test", "TC-001", "--full-docs"));
    }

    [Fact]
    public async Task TheSystemPart_HasNoTemplateComments_AndNoLeadingBlankLine()
    {
        var system = SystemPart(await PromptAsync("--suite", "s", "--test", "TC-001"));

        Assert.DoesNotContain("<!--", system);
        Assert.False(char.IsWhiteSpace(system[0]), "the system part starts with whitespace");
    }
}
