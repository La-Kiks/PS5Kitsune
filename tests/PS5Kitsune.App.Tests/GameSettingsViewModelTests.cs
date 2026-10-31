using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.App.Tests;

public sealed class GameSettingsViewModelTests : IDisposable
{
    private static readonly VulkanGpu Radeon = new("AMD Radeon(TM) Graphics", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD proprietary driver", "25.10.1", true, true);
    private static readonly VulkanGpu IrisXe = new("Intel(R) Iris(R) Xe Graphics", 0x8086, "integrated", "1.3.289", "IntelProprietaryWindows", "Intel Corporation", "101.5762", false, true);
    private static readonly VulkanGpu Dozen = new("Microsoft Direct3D12 (Intel(R) Iris(R) Xe Graphics)", 0x8086, "integrated", "1.2.335", "MesaDozen", "Dozen", "26.0", false, false);

    private readonly TestLibrary _library = new();
    private readonly FakeVulkanProbeRunner _probe = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Navigator _navigator = new();

    private static VulkanProbeOutcome Probe(VulkanProbeStatus status, params VulkanGpu[] gpus) => new(status, new VulkanProbeReport(true, null, gpus), null);

    private async Task<GameSettingsViewModel> OpenAsync(GameSettings? saved = null, string titleId = "PPSA99666")
    {
        if (!File.Exists(_library.Game("PPSA99666").RecordFile))
        {
            await _library.AddAsync("PPSA99666", "DOOM");
        }

        if (saved is not null)
        {
            await _library.Player.SaveSettingsAsync("PPSA99666", saved, TestContext.Current.CancellationToken);
        }

        var page = new GameSettingsViewModel(titleId, _library.Player, _library.Library, _library.Running, _probe, _dialogs, _navigator);
        _navigator.GoTo("library");
        _navigator.GoTo(page);
        await page.Loading;
        return page;
    }

    private GameSettings Saved() => _library.Library.Find("PPSA99666").Record.Settings;

    [Fact]
    public async Task The_lavapipe_help_comes_from_the_platform()
    {
        var page = await OpenAsync();

        Assert.Equal("Slow but works on any PC. " + FakeLavapipe.HowToGetText, page.LavapipeHelp);
    }

    [Fact]
    public async Task The_lavapipe_help_has_no_how_to_get_without_lavapipe()
    {
        using var library = new TestLibrary(withoutLavapipe: true);
        await library.AddAsync("PPSA99666", "DOOM");
        var page = new GameSettingsViewModel("PPSA99666", library.Player, library.Library, library.Running, _probe, _dialogs, _navigator);

        Assert.Equal("Slow but works on any PC.", page.LavapipeHelp);
    }

    [Fact]
    public async Task Shows_the_saved_settings_and_the_detected_gpus()
    {
        _probe.Outcome = Probe(VulkanProbeStatus.Succeeded, Radeon, IrisXe);

        var page = await OpenAsync(new GameSettings { Gpu = IrisXe.Name, Language = 2 });

        Assert.Equal("DOOM settings", page.Title);
        Assert.Equal(["Automatic", "AMD Radeon(TM) Graphics", "Intel(R) Iris(R) Xe Graphics (not supported)"], page.Gpus.Select(choice => choice.Label));
        Assert.Equal(IrisXe.Name, page.SelectedGpu?.Name);
        Assert.Equal(2, page.SelectedLanguage.Number);
        Assert.Equal("fifo", page.SelectedPresentMode.Value);
        Assert.Null(page.GpuStatus);
        Assert.True(page.CanEdit);
        Assert.False(page.ShowDisableDozen);
    }

    [Fact]
    public async Task A_saved_gpu_that_is_not_detected_is_kept_and_marked()
    {
        _probe.Outcome = Probe(VulkanProbeStatus.Succeeded, Radeon);

        var page = await OpenAsync(new GameSettings { Gpu = "GeForce" });

        Assert.Equal(["Automatic", "AMD Radeon(TM) Graphics", "GeForce (not found)"], page.Gpus.Select(choice => choice.Label));
        Assert.Equal("GeForce", page.SelectedGpu?.Name);
    }

    [Fact]
    public async Task A_failed_probe_keeps_automatic_and_the_saved_gpu()
    {
        _probe.Outcome = VulkanProbeOutcome.Failed("the driver crashed");

        var page = await OpenAsync(new GameSettings { Gpu = "GeForce" });

        Assert.Equal(["Automatic", "GeForce"], page.Gpus.Select(choice => choice.Label));
        Assert.Equal("Could not detect GPUs: the driver crashed", page.GpuStatus);
        Assert.True(page.CanEdit);
    }

    [Theory]
    [InlineData(VulkanProbeStatus.Succeeded, true, true)]
    [InlineData(VulkanProbeStatus.Succeeded, false, false)]
    [InlineData(VulkanProbeStatus.SucceededWithoutDozen, false, true)]
    public async Task Disable_dozen_shows_when_dozen_is_there_with_another_driver(VulkanProbeStatus status, bool dozenListed, bool shown)
    {
        _probe.Outcome = dozenListed ? Probe(status, Dozen, Radeon) : Probe(status, Radeon);

        var page = await OpenAsync();

        Assert.Equal(shown, page.ShowDisableDozen);
    }

    [Fact]
    public async Task Lavapipe_disables_the_gpu_and_dozen_choices()
    {
        var page = await OpenAsync();
        Assert.True(page.CanChooseGpu);

        page.Lavapipe = true;

        Assert.False(page.CanChooseGpu);
    }

    [Fact]
    public async Task The_overlay_position_is_chosen_only_with_the_overlay()
    {
        var page = await OpenAsync();
        Assert.False(page.CanChooseOverlayPosition);

        page.Overlay = true;

        Assert.True(page.CanChooseOverlayPosition);
    }

    [Fact]
    public async Task A_running_game_is_read_only()
    {
        await _library.AddAsync("PPSA99666", "DOOM");
        await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);

        var page = await OpenAsync();

        Assert.True(page.IsRunning);
        Assert.False(page.CanEdit);
        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.Equal("Running: changes can't be saved until it stops", page.RunningNotice);
    }

    [Fact]
    public async Task Save_writes_the_settings()
    {
        var page = await OpenAsync();
        page.Overlay = true;
        page.SelectedOverlayPosition = page.OverlayPositions[3];
        page.SelectedPresentMode = page.PresentModes[3];
        page.SelectedPlayGo = page.PlayGoChoices[1];

        await page.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Saved", page.Notice);
        Assert.Null(page.Message);
        Assert.Equal(new GameSettings { Overlay = true, OverlayPosition = "br", PresentMode = "immediate", PlayGoUnlistedChunks = "installed" }, Saved());
    }

    [Fact]
    public async Task Reset_fills_the_defaults_without_saving()
    {
        var page = await OpenAsync(new GameSettings { Language = 2, Lavapipe = true });

        page.ResetCommand.Execute(null);

        Assert.Equal(1, page.SelectedLanguage.Number);
        Assert.False(page.Lavapipe);
        Assert.Equal(2, Saved().Language);
    }

    [Fact]
    public async Task Back_with_unsaved_changes_asks_first()
    {
        var page = await OpenAsync();
        page.Overlay = true;

        await page.BackCommand.ExecuteAsync(null);
        Assert.Equal(["Your changes to the settings of DOOM are not saved."], _dialogs.Confirmations);
        Assert.Same(page, _navigator.CurrentPage);

        _dialogs.Confirm = true;
        await page.BackCommand.ExecuteAsync(null);
        Assert.Equal("library", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Back_is_leaving_while_the_discard_question_is_open()
    {
        var page = await OpenAsync();
        page.Overlay = true;
        _dialogs.ConfirmGate = new TaskCompletionSource<bool>();
        var window = new MainWindowViewModel(_navigator, _dialogs, new FakePages(), new PS5Kitsune.App.Shell.ThemeService(new PS5Kitsune.Core.Settings.AppSettingsStore(Path.Combine(Path.GetTempPath(), "ps5k-leaving")), _ => { }));

        var back = page.BackCommand.ExecuteAsync(null);

        Assert.True(page.IsLeaving);
        Assert.True(window.IsBusy);

        _dialogs.ConfirmGate.SetResult(true);
        await back;

        Assert.False(page.IsLeaving);
        Assert.False(window.IsBusy);
    }

    [Fact]
    public async Task Leaving_with_unsaved_changes_asks_first_and_can_be_declined()
    {
        var page = await OpenAsync();
        page.Overlay = true;
        var before = _navigator.CurrentPage;

        _dialogs.Confirm = false;
        Assert.False(await page.LeaveAsync());

        Assert.Same(before, _navigator.CurrentPage);
        Assert.Equal(["Your changes to the settings of DOOM are not saved."], _dialogs.Confirmations);
    }

    [Fact]
    public async Task Back_without_changes_goes_back_directly()
    {
        var page = await OpenAsync();

        await page.BackCommand.ExecuteAsync(null);

        Assert.Empty(_dialogs.Confirmations);
        Assert.Equal("library", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Saving_a_busy_game_says_so()
    {
        var page = await OpenAsync();
        page.Overlay = true;

        using (GameLock.TryAcquireNow(_library.Game("PPSA99666")))
        {
            await page.SaveCommand.ExecuteAsync(null);
        }

        Assert.Equal("DOOM is busy with another action. Try again.", page.Message);
        Assert.False(Saved().Overlay);
    }

    [Fact]
    public async Task Saving_a_game_started_meanwhile_says_so()
    {
        var page = await OpenAsync();
        page.Overlay = true;
        await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);

        await page.SaveCommand.ExecuteAsync(null);

        Assert.Equal("DOOM is running. Stop it to change its settings.", page.Message);
        Assert.True(page.IsRunning);
    }

    [Fact]
    public async Task The_page_becomes_editable_when_the_running_game_exits()
    {
        await _library.AddAsync("PPSA99666", "DOOM");
        var playing = await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);
        var page = await OpenAsync();
        Assert.True(page.IsRunning);

        ((FakeLaunchedGame)playing.Process).Exit(0);
        await page.Watching.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(page.IsRunning);
        Assert.True(page.CanEdit);
        Assert.True(page.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_refused_save_clears_once_the_game_exits()
    {
        var page = await OpenAsync();
        var playing = await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);
        page.Overlay = true;

        await page.SaveCommand.ExecuteAsync(null);
        Assert.Equal("DOOM is running. Stop it to change its settings.", page.Message);
        ((FakeLaunchedGame)playing.Process).Exit(0);
        await page.Watching.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(page.IsRunning);
        Assert.Null(page.Message);
    }

    [Fact]
    public async Task Reset_keeps_a_hidden_disable_dozen()
    {
        _probe.Outcome = Probe(VulkanProbeStatus.Succeeded, Radeon);
        var page = await OpenAsync(new GameSettings { DisableDozen = true, Language = 2 });
        Assert.False(page.ShowDisableDozen);

        page.ResetCommand.Execute(null);
        await page.SaveCommand.ExecuteAsync(null);

        Assert.True(Saved().DisableDozen);
        Assert.Equal(1, Saved().Language);
    }

    [Fact]
    public async Task Reset_clears_a_shown_disable_dozen()
    {
        _probe.Outcome = Probe(VulkanProbeStatus.Succeeded, Dozen, Radeon);
        var page = await OpenAsync(new GameSettings { DisableDozen = true });
        Assert.True(page.ShowDisableDozen);

        page.ResetCommand.Execute(null);

        Assert.False(page.DisableDozen);
    }

    [Fact]
    public async Task A_probe_that_throws_ends_the_detection_as_a_failure()
    {
        _probe.Failure = new InvalidOperationException("boom");

        var page = await OpenAsync();

        Assert.Equal("Could not detect GPUs: boom", page.GpuStatus);
        Assert.Equal(["Automatic"], page.Gpus.Select(choice => choice.Label));
        Assert.True(page.CanEdit);
    }

    [Fact]
    public async Task A_save_that_fails_says_why()
    {
        var page = await OpenAsync();
        await File.WriteAllTextAsync(_library.Game("PPSA99666").RecordFile, "{", TestContext.Current.CancellationToken);
        page.Overlay = true;

        await page.SaveCommand.ExecuteAsync(null);

        Assert.StartsWith("Could not save the settings of DOOM: ", page.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreadable_game_says_why()
    {
        var page = await OpenAsync(titleId: "PPSA00009");

        Assert.StartsWith("Cannot read the settings of PPSA00009: ", page.Message, StringComparison.Ordinal);
        Assert.False(page.CanEdit);
    }

    [Fact]
    public async Task Default_settings_follow_the_display_and_keep_advanced_closed()
    {
        var page = await OpenAsync();

        Assert.Equal(["Match the display", "1080p", "4K"], page.Resolutions.Select(choice => choice.Label));
        Assert.Null(page.SelectedResolution.Value);
        Assert.Equal("", page.ExtraEnvironmentText);
        Assert.False(page.IsAdvancedOpen);
    }

    [Fact]
    public async Task Shows_the_saved_resolution_and_extra_variables_with_advanced_open()
    {
        var page = await OpenAsync(new GameSettings
        {
            Resolution = "1080p",
            ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144", ["ANYPS5_A"] = "1" },
        });

        Assert.Equal("1080p", page.SelectedResolution.Value);
        Assert.Equal("ANYPS5_A=1\nAPS5_HOST_IMPORT_MIB=6144", page.ExtraEnvironmentText);
        Assert.Null(page.ExtraEnvironmentErrors);
        Assert.True(page.IsAdvancedOpen);
    }

    [Fact]
    public async Task Unlisted_chunks_alone_open_advanced()
    {
        var page = await OpenAsync(new GameSettings { PlayGoUnlistedChunks = "installed" });

        Assert.True(page.IsAdvancedOpen);
    }

    [Fact]
    public async Task A_bad_line_shows_its_error_and_blocks_save_until_fixed()
    {
        var page = await OpenAsync();

        page.ExtraEnvironmentText = "APS5_HOST_IMPORT_MIB=6144\nAPS5-HOST=1";

        Assert.Equal("Line 2: the name must start with APS5_ or ANYPS5_ and use only capital letters, digits and _.", page.ExtraEnvironmentErrors);
        Assert.False(page.SaveCommand.CanExecute(null));

        page.ExtraEnvironmentText = "APS5_HOST_IMPORT_MIB=6144";

        Assert.Null(page.ExtraEnvironmentErrors);
        Assert.True(page.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_writes_the_resolution_and_the_extra_variables()
    {
        var page = await OpenAsync();
        page.SelectedResolution = page.Resolutions.First(choice => choice.Value == "2160p");
        page.ExtraEnvironmentText = " APS5_HOST_IMPORT_MIB = 6144 \n";

        await page.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new GameSettings { Resolution = "2160p", ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144" } }, Saved());
        Assert.Equal("Saved", page.Notice);
    }

    [Fact]
    public async Task Reset_clears_the_resolution_and_the_extra_variables()
    {
        var page = await OpenAsync(new GameSettings { Resolution = "1080p", ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144" } });
        page.ExtraEnvironmentText = "bad";

        page.ResetCommand.Execute(null);

        Assert.Null(page.SelectedResolution.Value);
        Assert.Equal("", page.ExtraEnvironmentText);
        Assert.Null(page.ExtraEnvironmentErrors);
    }

    [Fact]
    public async Task Leaving_with_an_invalid_line_asks_first()
    {
        var page = await OpenAsync();
        page.ExtraEnvironmentText = "bad";

        _dialogs.Confirm = false;

        Assert.False(await page.LeaveAsync());
        Assert.Equal(["Your changes to the settings of DOOM are not saved."], _dialogs.Confirmations);
    }

    [Fact]
    public async Task Leaving_after_reformatting_the_same_variables_does_not_ask()
    {
        var page = await OpenAsync(new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144" } });
        page.ExtraEnvironmentText = "  APS5_HOST_IMPORT_MIB = 6144\n\n";

        Assert.True(await page.LeaveAsync());
        Assert.Empty(_dialogs.Confirmations);
    }

    public void Dispose() => _library.Dispose();
}
