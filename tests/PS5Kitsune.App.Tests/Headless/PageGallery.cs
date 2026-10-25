using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.App.Tests.Headless;

/// <summary>
/// Builds every page in a representative state with fake data, for the focus and screenshot tests. The pages are built as the view model
/// tests build them. Running work is cancelled in <see cref="Dispose"/>.
/// </summary>
internal sealed class PageGallery : IDisposable
{
    private const string Doom = "PPSA99666";

    private static readonly AnyPS5InstallRecord Record = new(1, "test-profile", AnyPS5Mode.Release, "v0.1.2", null, new Uri("https://example.test/u.py"), new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    private static readonly VulkanGpu Radeon = new("AMD Radeon(TM) Graphics", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD proprietary driver", "25.10.1", true, true);

    private static readonly VulkanGpu IrisXe = new("Intel(R) Iris(R) Xe Graphics", 0x8086, "integrated", "1.3.289", "IntelProprietaryWindows", "Intel Corporation", "101.5762", false, true);

    private readonly TempDirectory _temp = new();
    private readonly List<TestLibrary> _libraries = [];
    private readonly List<Func<Task>> _cleanups = [];

    /// <summary>A folder in the gallery's temp directory, for a settings store.</summary>
    public string SettingsFolder => _temp.Combine("settings");

    public async Task<IReadOnlyList<(string Name, object Page)>> BuildAsync() =>
    [
        ("Home", await HomeAsync()),
        ("SystemCheck", await SystemCheckAsync()),
        ("Mode", Mode()),
        ("Tools", Tools()),
        ("Install-Running", InstallRunning()),
        ("Install-Failed", await InstallFailedAsync()),
        ("Done", Done()),
        ("ChooseGame", ChooseGame()),
        ("GameSummary", GameSummary()),
        ("AddGame", AddGame()),
        ("AddResult", AddResult()),
        ("Reconvert", Reconvert()),
        ("Library", await LibraryAsync(withGames: true)),
        ("Library-Empty", await LibraryAsync(withGames: false)),
        ("Game-Preparing", await GamePreparingAsync()),
        ("Game-Running-NoLog", await GameRunningAsync(logLines: 0)),
        ("Game-Running-Log", await GameRunningAsync(logLines: 20)),
        ("Game-Diagnosis", await GameDiagnosisAsync()),
        ("GameSettings", await GameSettingsAsync()),
    ];

    public void Dispose()
    {
        foreach (var cleanup in _cleanups)
        {
            cleanup().GetAwaiter().GetResult();
        }

        foreach (var library in _libraries)
        {
            library.Dispose();
        }

        _temp.Dispose();
    }

    private async Task<HomeViewModel> HomeAsync()
    {
        var installer = new FakeInstaller { State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record) };
        var sessions = new SessionHost(root => TestSessions.Create(root, installer), _temp.Combine("home-root"));
        var viewModel = new HomeViewModel(sessions, new AppSettingsStore(_temp.Combine("home-settings")), new FakeDialogs(), new Navigator(), new FakePages(), new TestPlatform());
        await viewModel.RefreshAsync();
        return viewModel;
    }

    private static async Task<SystemCheckViewModel> SystemCheckAsync()
    {
        var report = new SystemCheckReport(
            [
                new CheckResult("os-arch", "64-bit x86 system", CheckStatus.Ok, "64-bit x86 system."),
                new CheckResult("commit-limit", "Memory commit limit", CheckStatus.Warning, "Too low.", "Enlarge the page file.", CheckFix.OpenPerformanceSettings),
                new CheckResult("disk", "Free disk space", CheckStatus.Blocking, "Only 2 GiB are free.", "Free some space, or choose another data folder."),
            ],
            new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, new VulkanProbeReport(true, null, [Radeon]), null));
        var viewModel = new SystemCheckViewModel(new FakeChecker(report), AnyPS5Mode.Release, new TestPlatform(), new Navigator(), new FakePages());
        await viewModel.CheckCommand.ExecuteAsync(null);
        return viewModel;
    }

    private static ModeViewModel Mode()
    {
        var profile = TestSessions.Profile with
        {
            Build = new ProfileBuild(new Uri("https://github.com/boykopovar/AnyPS5.git"), "3fd1cc393b8512c2e989860053f30dee5a8cd1aa"),
            Checks = new CheckThresholds { BuildMinFreeDiskGiB = 10 },
        };
        return new ModeViewModel(profile, new FakeInstaller { CurrentMode = AnyPS5Mode.Release }, new Navigator(), new FakePages());
    }

    private static ToolsViewModel Tools()
    {
        var profile = TestSessions.Profile with
        {
            Tools = new Dictionary<string, ToolDefinition>
            {
                ["python"] = Definition("3.14.8", 12601679),
                ["mingit"] = Definition("2.56.0.2", 39806486),
                ["winlibs"] = Definition("15.2.0-r7", 267593703),
            },
        };
        var viewModel = new ToolsViewModel(
            new BuildToolsSummary(profile, [new FakeTool("python", true), new FakeTool("mingit", false), new FakeTool("winlibs", false)]), new Navigator(), new FakePages());
        viewModel.OnNavigatedTo();
        return viewModel;
    }

    private static ToolDefinition Definition(string version, long? size) =>
        new(version, new Uri("https://example.test/t.zip"), new string('a', 64), size);

    private InstallViewModel InstallRunning()
    {
        var installer = new FakeInstaller("Install Python", "Download the relinker", "Build the tools")
        {
            Install = async (_, onEvent, token) =>
            {
                onEvent!(new StepEvent(StepEventKind.Skipped, "Install Python", 1, 3));
                onEvent(new StepEvent(StepEventKind.Started, "Download the relinker", 2, 3));
                onEvent(new StepEvent(StepEventKind.Completed, "Download the relinker", 2, 3, TimeSpan.FromSeconds(1.5)));
                onEvent(new StepEvent(StepEventKind.Started, "Build the tools", 3, 3));
                await Task.Delay(Timeout.Infinite, token);
            },
        };
        var viewModel = new InstallViewModel(installer, AnyPS5Mode.Release, "C:/root/logs/app.log", new TestPlatform(), new InlineDispatcher(), new Navigator(), new FakePages());
        Run(viewModel);
        return viewModel;
    }

    private static async Task<InstallViewModel> InstallFailedAsync()
    {
        var installer = new FakeInstaller("Install Python", "Download the relinker")
        {
            Install = (_, onEvent, _) =>
            {
                var cause = new IOException("disk gone");
                onEvent!(new StepEvent(StepEventKind.Completed, "Install Python", 1, 2, TimeSpan.FromSeconds(2)));
                onEvent(new StepEvent(StepEventKind.Started, "Download the relinker", 2, 2));
                onEvent(new StepEvent(StepEventKind.Failed, "Download the relinker", 2, 2, TimeSpan.Zero, cause));
                throw new StepFailedException("Download the relinker", cause);
            },
        };
        var viewModel = new InstallViewModel(installer, AnyPS5Mode.Release, "C:/root/logs/app.log", new TestPlatform(), new InlineDispatcher(), new Navigator(), new FakePages());
        await viewModel.RunAsync();
        return viewModel;
    }

    private DoneViewModel Done() =>
        new(Record, new AnyPS5Paths(new DataLayout(_temp.Combine("done-root")), new TestPlatform()), "app.log", new TestPlatform(), new Navigator(), new FakePages());

    private static ChooseGameViewModel ChooseGame()
    {
        var navigator = new Navigator();
        var viewModel = new ChooseGameViewModel(new FakeGameImporter(), new FakeDialogs(), navigator, new FakePages(), NullLogger<ChooseGameViewModel>.Instance);
        navigator.GoTo(viewModel);
        return viewModel;
    }

    private static GameSummaryViewModel GameSummary()
    {
        var navigator = new Navigator();
        navigator.GoTo("choose");
        var game = TestGameData.Prepared(issues: [new ValidationIssue(IssueSeverity.Warning, "Low disk space.")]);
        var viewModel = new GameSummaryViewModel(game, false, new TestPlatform(), navigator, new FakePages());
        navigator.GoTo(viewModel);
        return viewModel;
    }

    private AddGameViewModel AddGame()
    {
        var importer = new FakeGameImporter
        {
            Add = async (_, _, _, onEvent, token) =>
            {
                onEvent!(new StepEvent(StepEventKind.Completed, "Copy the game", 1, 6, TimeSpan.FromSeconds(4.2)));
                onEvent(new StepEvent(StepEventKind.Completed, "Copy the AnyPS5 libraries", 2, 6, TimeSpan.FromSeconds(0.8)));
                onEvent(new StepEvent(StepEventKind.Started, "Unwrap SELF files", 3, 6));
                await Task.Delay(Timeout.Infinite, token);
                return TestGameData.Added();
            },
        };
        var viewModel = new AddGameViewModel(importer, TestGameData.Prepared(), false, "app.log", new TestPlatform(), new InlineDispatcher(), new Navigator(), new FakePages());
        Run(viewModel);
        return viewModel;
    }

    private static AddResultViewModel AddResult()
    {
        var audit = new AuditSummary(360, 4, 0, new Dictionary<string, int>())
        {
            Unresolved =
            [
                new UnresolvedImport("__tls_get_addr_nid_postfix", "libc.prx", AuditClass.Stub),
                new UnresolvedImport("sceSystemServiceLoadExec", "libSceSystemService.prx", AuditClass.Stub),
            ],
        };
        var added = TestGameData.Added(audit, [new ValidationIssue(IssueSeverity.Warning, "Low disk space.")]);
        return new AddResultViewModel(added, new TestPlatform(), new Navigator(), new FakePages());
    }

    private ReconvertViewModel Reconvert()
    {
        var reconverter = new FakeGameReconverter
        {
            Reconvert = async (_, _, onEvent, token) =>
            {
                onEvent!(new StepEvent(StepEventKind.Completed, "Set the old conversion aside", 1, 6, TimeSpan.FromSeconds(0.3)));
                onEvent(new StepEvent(StepEventKind.Started, "Copy the AnyPS5 libraries", 2, 6));
                await Task.Delay(Timeout.Infinite, token);
                return TestGameData.Reconverted();
            },
        };
        var viewModel = new ReconvertViewModel(reconverter, Doom, "DOOM", "app.log", new TestPlatform(), new InlineDispatcher(), new Navigator(), new FakePages());
        Run(viewModel);
        return viewModel;
    }

    // Starts the run and cancels it, waiting for the clean-up, when the gallery is disposed.
    private void Run(StepRunViewModel viewModel)
    {
        _ = viewModel.RunAsync();
        _cleanups.Add(viewModel.CancelAndWaitAsync);
    }

    private TestLibrary NewLibrary()
    {
        var library = new TestLibrary();
        _libraries.Add(library);
        return library;
    }

    private async Task<LibraryViewModel> LibraryAsync(bool withGames)
    {
        var library = NewLibrary();
        if (withGames)
        {
            var build = new ConversionInfo("test-profile", "build", "3fd1cc3", new string('3', 40), false);
            await AddGameAsync(library, "PPSA00001", "Astro Bot", AuditVerdict.Green, withIcon: true, lastPlayedAt: new DateTimeOffset(2026, 10, 24, 20, 0, 0, TimeSpan.Zero));
            await AddGameAsync(library, "PPSA00002", "Returnal", AuditVerdict.Orange, withIcon: true, convertedWith: build, lastPlayedAt: new DateTimeOffset(2026, 10, 23, 20, 0, 0, TimeSpan.Zero));
            await AddGameAsync(library, "PPSA00003", "Gran Turismo 7", AuditVerdict.Green, withIcon: false, lastPlayedAt: new DateTimeOffset(2026, 10, 22, 20, 0, 0, TimeSpan.Zero));
            await AddGameAsync(library, "PPSA00004", "Demons Souls", AuditVerdict.Red, withIcon: false);
            await library.Player.LaunchAsync("PPSA00001", CancellationToken.None);
        }

        var page = new LibraryViewModel(library.Player, library.Cleaner, library.Running, library.Installer, library.Platform, library.Dialogs, new Navigator(), new FakePages());
        await page.RefreshAsync();
        return page;
    }

    private static async Task AddDoomAsync(TestLibrary library, GameSettings? settings = null)
    {
        await library.AddAsync(Doom, "DOOM");
        if (settings is not null)
        {
            await library.Player.SaveSettingsAsync(Doom, settings, CancellationToken.None);
        }
    }

    private async Task<GameViewModel> GamePreparingAsync()
    {
        var library = NewLibrary();
        await AddDoomAsync(library, new GameSettings { Lavapipe = true });
        library.Lavapipe.Reports = [35L << 20];
        library.Lavapipe.Gate = new TaskCompletionSource();
        var page = new GameViewModel(library.Game(Doom), "DOOM", true, library.Player, library.Platform, new InlineDispatcher(), new Navigator(), new FakePages());
        page.OnNavigatedTo();
        _cleanups.Add(async () =>
        {
            page.CancelLaunchCommand.Execute(null);
            await page.Launching;
        });
        await WaitUntilAsync(() => page.StatusText.StartsWith("Downloading", StringComparison.Ordinal));
        return page;
    }

    private async Task<GameViewModel> GameRunningAsync(int logLines)
    {
        var (page, process) = await LaunchAsync(diagnose: false);
        if (logLines > 0)
        {
            await File.WriteAllLinesAsync(process.LogFile, Enumerable.Range(1, logLines).Select(index => $"[anyps5] frame {index}: presented in 16.{index % 10} ms"));
        }

        page.OnNavigatedTo();
        await WaitUntilAsync(() => page.IsRunning && page.Log.Count == logLines);
        return page;
    }

    private async Task<GameViewModel> GameDiagnosisAsync()
    {
        var (page, process) = await LaunchAsync(diagnose: true);
        await File.AppendAllTextAsync(process.LogFile, "Vulkan: buffer device address and shaderInt64 are required\n");
        process.Exit(1);
        page.OnNavigatedTo();
        await page.Following.WaitAsync(TimeSpan.FromSeconds(10));
        return page;
    }

    private async Task<(GameViewModel Page, FakeLaunchedGame Process)> LaunchAsync(bool diagnose)
    {
        var library = NewLibrary();
        await AddDoomAsync(library);
        var playing = await library.Player.LaunchAsync(Doom, CancellationToken.None);
        var page = new GameViewModel(
            playing, false, library.Player, library.Platform, new InlineDispatcher(), new Navigator(), new FakePages(), null, diagnose ? library.Diagnosis() : null);
        return (page, (FakeLaunchedGame)playing.Process);
    }

    private async Task<GameSettingsViewModel> GameSettingsAsync()
    {
        var library = NewLibrary();
        await AddDoomAsync(library);
        var probe = new FakeVulkanProbeRunner { Outcome = new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, new VulkanProbeReport(true, null, [Radeon, IrisXe]), null) };
        var navigator = new Navigator();
        var page = new GameSettingsViewModel(Doom, library.Player, library.Library, library.Running, probe, library.Dialogs, navigator);
        navigator.GoTo("library");
        navigator.GoTo(page);
        await page.Loading;
        return page;
    }

    // Rewrites the record an add leaves, with the audit verdict and icon the library should show.
    private static async Task AddGameAsync(TestLibrary library, string titleId, string name, AuditVerdict verdict, bool withIcon, ConversionInfo? convertedWith = null, DateTimeOffset? lastPlayedAt = null)
    {
        var game = await library.AddAsync(titleId, name, convertedWith, lastPlayedAt);
        var record = library.Library.Find(titleId).Record;
        var audit = verdict switch
        {
            AuditVerdict.Green => new AuditRecord(364, 0, 0, verdict),
            AuditVerdict.Orange => new AuditRecord(360, 4, 0, verdict),
            _ => new AuditRecord(300, 4, 60, verdict),
        };
        if (withIcon)
        {
            await File.WriteAllBytesAsync(Path.Combine(game.Root, "icon0.png"), Png(titleId.Length + titleId[^1]));
        }

        await GameRecordStore.WriteAsync(game.RecordFile, record with { Audit = audit, Icon = withIcon ? "icon0.png" : null }, CancellationToken.None);
    }

    // A 64x64 RGB PNG with a diagonal gradient, written by hand so no UI thread is needed.
    private static byte[] Png(int seed)
    {
        const int size = 64;
        var raw = new byte[size * ((size * 3) + 1)];
        var offset = 0;
        for (var y = 0; y < size; y++)
        {
            raw[offset++] = 0;
            for (var x = 0; x < size; x++)
            {
                raw[offset++] = (byte)(((x * 4) + (seed * 40)) & 0xFF);
                raw[offset++] = (byte)(y * 4);
                raw[offset++] = (byte)(255 - ((x + y) * 2));
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        WriteInt(header, 0, size);
        WriteInt(header, 4, size);
        header[8] = 8;
        header[9] = 2;
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        var body = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type, body);
        data.CopyTo(body, 4);
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        stream.Write(length);
        stream.Write(body);
        var crc = new byte[4];
        WriteInt(crc, 0, (int)Crc32(body));
        stream.Write(crc);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }

    // The launch runs on the thread pool; its progress reaches the page through the (inline) dispatcher.
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("A gallery page did not reach its state.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class FakeChecker(SystemCheckReport report) : ISystemChecker
    {
        public Task<SystemCheckReport> RunAsync(AnyPS5Mode mode, CancellationToken cancellationToken = default) => Task.FromResult(report);
    }
}
