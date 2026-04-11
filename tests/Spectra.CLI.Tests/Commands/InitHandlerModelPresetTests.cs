using System.Text.Json;
using Microsoft.Extensions.Logging;
using Spectra.CLI.Commands.Init;
using Spectra.CLI.Infrastructure;
using Spectra.Core.Models.Config;

namespace Spectra.CLI.Tests.Commands;

/// <summary>
/// Spec 041 tests: new default models (gpt-4.1 / gpt-5-mini) and the
/// ModelPreset machinery used by <c>spectra init -i</c>.
/// </summary>
public class InitHandlerModelPresetTests : IDisposable
{
    private readonly string _testDir;
    private readonly ILogger<InitHandler> _logger;

    public InitHandlerModelPresetTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "spectra-preset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        var loggerFactory = LoggingSetup.CreateLoggerFactory(VerbosityLevel.Quiet);
        _logger = loggerFactory.CreateLogger<InitHandler>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task Default_WritesGpt41AndGpt5Mini()
    {
        var handler = new InitHandler(_logger, _testDir);
        var exitCode = await handler.HandleAsync(force: false);
        Assert.Equal(ExitCodes.Success, exitCode);

        var config = LoadConfig();
        var primary = config.Ai.Providers.First(p => p.Enabled);
        Assert.Equal("github-models", primary.Name);
        Assert.Equal("gpt-4.1", primary.Model);

        Assert.NotNull(config.Ai.Critic);
        Assert.True(config.Ai.Critic!.Enabled);
        Assert.Equal("github-models", config.Ai.Critic.Provider);
        Assert.Equal("gpt-5-mini", config.Ai.Critic.Model);
    }

    [Fact]
    public void ModelPresetAll_FirstPresetIsDefaultGpt41()
    {
        var first = ModelPreset.All[0];
        Assert.Equal("gpt-4.1", first.GeneratorModel);
        Assert.Equal("gpt-5-mini", first.CriticModel);
        Assert.Equal("github-models", first.GeneratorProvider);
        Assert.Equal("github-models", first.CriticProvider);
        Assert.False(first.IsCustom);
    }

    [Fact]
    public void ModelPresetAll_ContainsFourPresets()
    {
        Assert.Equal(4, ModelPreset.All.Count);
        Assert.True(ModelPreset.All[3].IsCustom);
    }

    [Fact]
    public void ModelPresetAll_Preset2_SonnetPlusGpt41()
    {
        var p = ModelPreset.All[1];
        Assert.Equal("claude-sonnet-4.5", p.GeneratorModel);
        Assert.Equal("gpt-4.1", p.CriticModel);
        Assert.False(p.IsCustom);
    }

    [Fact]
    public void ModelPresetAll_Preset3_Gpt41PlusHaiku()
    {
        var p = ModelPreset.All[2];
        Assert.Equal("gpt-4.1", p.GeneratorModel);
        Assert.Equal("claude-haiku-4.5", p.CriticModel);
        Assert.False(p.IsCustom);
    }

    [Fact]
    public async Task ApplyModelPreset_Preset2_RewritesGeneratorAndCritic()
    {
        // Arrange — seed a config file with the default template.
        var handler = new InitHandler(_logger, _testDir);
        var exitCode = await handler.HandleAsync(force: false);
        Assert.Equal(ExitCodes.Success, exitCode);

        // Act — apply preset 2 (Sonnet + GPT-4.1 critic).
        var configPath = Path.Combine(_testDir, "spectra.config.json");
        await InitHandler.ApplyModelPresetAsync(configPath, ModelPreset.All[1], CancellationToken.None);

        // Assert — config now reflects the preset.
        var config = LoadConfig();
        var primary = config.Ai.Providers.First(p => p.Enabled);
        Assert.Equal("github-models", primary.Name);
        Assert.Equal("claude-sonnet-4.5", primary.Model);
        Assert.NotNull(config.Ai.Critic);
        Assert.Equal("gpt-4.1", config.Ai.Critic!.Model);
        Assert.True(config.Ai.Critic.Enabled);
    }

    [Fact]
    public async Task ApplyModelPreset_Preset3_RewritesCriticOnly()
    {
        var handler = new InitHandler(_logger, _testDir);
        await handler.HandleAsync(force: false);

        var configPath = Path.Combine(_testDir, "spectra.config.json");
        await InitHandler.ApplyModelPresetAsync(configPath, ModelPreset.All[2], CancellationToken.None);

        var config = LoadConfig();
        var primary = config.Ai.Providers.First(p => p.Enabled);
        Assert.Equal("gpt-4.1", primary.Model);
        Assert.Equal("claude-haiku-4.5", config.Ai.Critic!.Model);
    }

    [Fact]
    public async Task ApplyModelPreset_PreservesOtherAiFields()
    {
        // After applying a preset, unrelated providers and top-level fields
        // must remain intact — the preset only rewrites providers[0].name /
        // providers[0].model / providers[0].enabled and ai.critic.
        var handler = new InitHandler(_logger, _testDir);
        await handler.HandleAsync(force: false);

        var configPath = Path.Combine(_testDir, "spectra.config.json");
        var before = await File.ReadAllTextAsync(configPath);
        Assert.Contains("fallback_strategy", before);

        await InitHandler.ApplyModelPresetAsync(configPath, ModelPreset.All[1], CancellationToken.None);

        var after = await File.ReadAllTextAsync(configPath);
        Assert.Contains("fallback_strategy", after);
        // Disabled alternate providers (openai / anthropic rows in the template)
        // must still be present.
        Assert.Contains("\"name\": \"openai\"", after);
        Assert.Contains("\"name\": \"anthropic\"", after);
    }

    [Fact]
    public async Task ExistingConfig_NotOverwrittenWithoutForce()
    {
        var handler = new InitHandler(_logger, _testDir);
        await handler.HandleAsync(force: false);

        // Tamper with the config so we can detect overwrite.
        var configPath = Path.Combine(_testDir, "spectra.config.json");
        var original = await File.ReadAllTextAsync(configPath);
        var tampered = original.Replace("gpt-4.1", "deepseek-v3.2");
        await File.WriteAllTextAsync(configPath, tampered);

        // Second init without --force should fail and leave the config alone.
        var handler2 = new InitHandler(_logger, _testDir);
        var exitCode = await handler2.HandleAsync(force: false);
        Assert.Equal(ExitCodes.Error, exitCode);

        var stillTampered = await File.ReadAllTextAsync(configPath);
        Assert.Contains("deepseek-v3.2", stillTampered);
    }

    private SpectraConfig LoadConfig()
    {
        var path = Path.Combine(_testDir, "spectra.config.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SpectraConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("config did not deserialize");
    }
}
