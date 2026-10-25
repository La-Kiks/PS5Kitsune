using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.App.Tests;

internal sealed class TestPlatform : IPlatform
{
    public List<CheckFix> OpenedFixes { get; } = [];

    public List<string> OpenedPaths { get; } = [];

    /// <summary>Thrown by OpenPath and OpenFix when set.</summary>
    public Exception? OpenFailure { get; set; }

    public string Name => "Test";

    public string DefaultDataRoot => "unused";

    public string SettingsDirectory => "unused";

    public string ExecutableSuffix => ".exe";

    public ulong? GetCommitLimitBytes() => null;

    public IReadOnlyList<string> GetVulkanDriverManifests() => [];

    public void OpenFix(CheckFix fix)
    {
        if (OpenFailure is not null)
        {
            throw OpenFailure;
        }

        OpenedFixes.Add(fix);
    }

    public void OpenPath(string path)
    {
        if (OpenFailure is not null)
        {
            throw OpenFailure;
        }

        OpenedPaths.Add(path);
    }
}

internal delegate Task InstallBehavior(IProgress<StepProgress>? progress, Action<StepEvent>? onEvent, CancellationToken cancellationToken);

internal sealed class FakeInstaller(params string[] stepNames) : IAnyPS5Installer
{
    /// <summary>The names CreateSteps returns; a test changes them between runs.</summary>
    public string[] StepNames { get; set; } = stepNames;

    public AnyPS5InstallState State { get; set; } = new(AnyPS5InstallStatus.NotInstalled, null);

    public InstallBehavior Install { get; set; } = (_, _, _) => Task.CompletedTask;

    public int Runs { get; private set; }

    public AnyPS5Mode CurrentMode { get; set; } = AnyPS5Mode.Release;

    public AnyPS5Mode? RequestedMode { get; set; }

    /// <summary>False simulates a profile without a build section.</summary>
    public bool BuildSupported { get; set; } = true;

    public bool Supports(AnyPS5Mode mode) => mode == AnyPS5Mode.Release || BuildSupported;

    public AnyPS5Mode? InstalledMode { get; private set; }

    /// <summary>When set, ReadRecord() blocks on it (at most 5 s, so a broken test cannot hang): the library read stays pending.</summary>
    public ManualResetEventSlim? ReadRecordGate { get; set; }

    /// <summary>Thrown by ReadRecord() when set: the library read fails.</summary>
    public Exception? ReadRecordFailure { get; set; }

    public AnyPS5InstallRecord? ReadRecord()
    {
        ReadRecordGate?.Wait(TimeSpan.FromSeconds(5));
        if (ReadRecordFailure is not null)
        {
            throw ReadRecordFailure;
        }

        return State.Record;
    }

    public AnyPS5Mode? ActiveMode { get; set; }

    /// <summary>Records by mode; a mode not listed falls back to State.Record when that record has the mode.</summary>
    public Dictionary<AnyPS5Mode, AnyPS5InstallRecord> Records { get; } = [];

    /// <summary>The modes passed to Use, in order.</summary>
    public List<AnyPS5Mode> Used { get; } = [];

    /// <summary>Thrown by MigrateLegacyInstall when set.</summary>
    public Exception? MigrateFailure { get; set; }

    public int Migrations { get; private set; }

    public void MigrateLegacyInstall()
    {
        Migrations++;
        if (MigrateFailure is not null)
        {
            throw MigrateFailure;
        }
    }

    public AnyPS5InstallRecord? ReadRecord(AnyPS5Mode mode) =>
        Records.TryGetValue(mode, out var record) ? record : State.Record is { } stateRecord && stateRecord.Mode == mode ? stateRecord : null;

    /// <summary>Thrown by Use when set, before ActiveMode changes.</summary>
    public Exception? UseFailure { get; set; }

    public void Use(AnyPS5Mode mode)
    {
        if (UseFailure is not null)
        {
            throw UseFailure;
        }

        Used.Add(mode);
        ActiveMode = mode;
    }

    public IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode) => [.. StepNames.Select(name => new NamedStep(name))];

    /// <summary>When set, GetState blocks on it (at most 5 s, so a broken test cannot hang) after reading State.</summary>
    public ManualResetEventSlim? GetStateGate { get; set; }

    /// <summary>Thrown by GetState when set.</summary>
    public Exception? GetStateFailure { get; set; }

    /// <summary>Released each time GetState is entered, so a test knows a check is running.</summary>
    public SemaphoreSlim GetStateEntered { get; } = new(0);

    /// <summary>The modes passed to GetState, in order.</summary>
    public List<AnyPS5Mode> CheckedModes { get; } = [];

    public AnyPS5InstallState GetState(AnyPS5Mode mode)
    {
        CheckedModes.Add(mode);
        var state = State;
        var gate = GetStateGate;
        var failure = GetStateFailure;
        GetStateEntered.Release();
        gate?.Wait(TimeSpan.FromSeconds(5));
        return failure is null ? state : throw failure;
    }

    public Task InstallAsync(AnyPS5Mode mode, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        InstalledMode = mode;
        Runs++;
        return Install(progress, onEvent, cancellationToken);
    }

    private sealed class NamedStep(string name) : IStep
    {
        public string Name => name;

        public bool IsAlreadyDone() => false;

        public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

/// <summary>Pages are plain strings, so a test asserts where the navigator went, and with which mode.</summary>
internal sealed class FakePages : IPages
{
    public object Home() => "home";

    public object Mode() => "mode";

    public object SystemCheck(AnyPS5Mode mode) => $"check:{mode}";

    public object Tools() => "tools";

    public object Install(AnyPS5Mode mode) => $"install:{mode}";

    public object Done() => "done";

    public object ChooseGame() => "choose";

    public object GameSummary(PreparedGame game) => $"summary:{game.Metadata?.TitleId}";

    public object AddGame(PreparedGame game, bool toIntel) => $"adding:{game.Metadata?.TitleId}:{toIntel}";

    public object AddResult(GameAdded added) => $"result:{added.Record.TitleId}";

    public object Reconvert(string titleId, string name) => $"reconvert:{titleId}";

    public object ReconvertResult(GameReconverted reconverted) => $"reconverted:{reconverted.Record.TitleId}";

    public object Library() => "library";

    public object Game(PlayingGame playing, bool firstLaunch) => $"game:{playing.Record.TitleId}";

    public object Launch(GameLayout game, string name, bool firstLaunch) => $"launch:{game.TitleId}";

    public object GameSettings(string titleId) => $"settings:{titleId}";
}

internal sealed class FakeTool(string id, bool installed) : IPortableTool
{
    public string Id => id;

    public bool Installed { get; set; } = installed;

    public bool IsInstalled() => Installed;

    public Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<string> GetVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult("1");
}

internal sealed class FakeDialogs : IDialogService
{
    public string? Folder { get; set; }

    public string? File { get; set; }

    public Task<string?> PickFileAsync(string title, string extension) => Task.FromResult(File);

    public bool Confirm { get; set; }

    public List<string> Confirmations { get; } = [];

    public Task<string?> PickFolderAsync(string title, string? startFolder) => Task.FromResult(Folder);

    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Confirmations.Add(message);
        return Task.FromResult(Confirm);
    }

    public List<string> Copied { get; } = [];

    /// <summary>Thrown by CopyTextAsync when set.</summary>
    public Exception? CopyFailure { get; set; }

    public Task CopyTextAsync(string text)
    {
        if (CopyFailure is not null)
        {
            throw CopyFailure;
        }

        Copied.Add(text);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCpuInfo : ICpuInfo
{
    public CpuFacts Read() => new("AuthenticAMD", []);
}

internal sealed class InlineDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

internal static class TestSessions
{
    public static readonly AnyPS5Profile Profile = new()
    {
        SchemaVersion = AnyPS5Profile.CurrentSchemaVersion,
        Id = "test-profile",
        Validated = true,
        Source = new ProfileSource("release", Tag: "v0.1.2", PublishedAt: new DateTimeOffset(2026, 10, 14, 8, 56, 52, TimeSpan.Zero)),
    };

    /// <summary>A session whose services are the given installer and the test profile; nothing is logged.</summary>
    public static RootSession Create(string root, IAnyPS5Installer installer)
    {
        var services = new ServiceCollection();
        services.AddSingleton(installer);
        services.AddSingleton(Profile);
        services.AddSingleton(new GameLibrary(new DataLayout(root), new TestPlatform(), TimeProvider.System, NullLogger<GameLibrary>.Instance));
        return new RootSession(new DataLayout(root), services.BuildServiceProvider(), Path.Combine(root, "logs", "app.log"));
    }
}

/// <summary>Builds prepared games and add results without touching the disk.</summary>
internal static class TestGameData
{
    public const string TitleId = "PPSA99666";

    public static PreparedGame Prepared(
        IReadOnlyList<ValidationIssue>? issues = null,
        bool hasMetadata = true,
        IReadOnlyList<GameBinary>? binaries = null,
        string? icon = null,
        bool alreadyAdded = false,
        bool resumes = false,
        IReadOnlyList<string>? moduleDirectories = null,
        long sizeBytes = 2_359_296)
    {
        var metadata = hasMetadata ? new GameMetadata(TitleId, "DOOM") : null;
        var validation = new GameValidation(metadata, moduleDirectories ?? ["sce_module"], sizeBytes, issues ?? []);
        return new PreparedGame(
            new GameSource(@"C:\games\doom", null),
            validation,
            icon,
            binaries ?? [new GameBinary("eboot.bin", BinaryKind.Self), new GameBinary("sce_module/libc.prx", BinaryKind.Self)],
            hasMetadata ? $@"C:\root\games\{TitleId}" : null,
            alreadyAdded,
            resumes);
    }

    public static GameReconverted Reconverted()
    {
        var added = Added(new AuditSummary(361, 3, 0, new Dictionary<string, int>()));
        return new GameReconverted(added.Record, added.GameDirectory, added.Audit, new AuditRecord(360, 4, 0, AuditVerdict.Orange));
    }

    public static GameAdded Added(AuditSummary? audit = null, IReadOnlyList<ValidationIssue>? warnings = null)
    {
        var summary = audit ?? new AuditSummary(360, 4, 0, new Dictionary<string, int>());
        var record = new GameRecord
        {
            SchemaVersion = GameRecord.CurrentSchemaVersion,
            TitleId = TitleId,
            Name = "DOOM",
            Executable = TitleId + ".exe",
            ConvertedWith = new ConversionInfo("test-profile", "release", "v0.1.2", null, false),
            Audit = new AuditRecord(summary.Implemented, summary.Stub, summary.Absent, summary.Verdict),
            AddedAt = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero),
        };
        return new GameAdded(record, $@"C:\root\games\{TitleId}", warnings ?? [], summary);
    }
}

internal delegate Task<GameAdded> AddBehavior(PreparedGame game, bool toIntel, IProgress<StepProgress>? progress, Action<StepEvent>? onEvent, CancellationToken cancellationToken);

internal sealed class FakeGameImporter : IGameImporter
{
    public IReadOnlyList<string> StepNames { get; } =
        ["Copy the game", "Copy the AnyPS5 libraries", "Unwrap SELF files", "Convert with the relinker", "Audit the imports", "Record the game"];

    /// <summary>What PrepareAsync does; by default returns <see cref="TestGameData.Prepared"/>.</summary>
    public Func<string, CancellationToken, Task<PreparedGame>> Prepare { get; set; } = (_, _) => Task.FromResult(TestGameData.Prepared());

    public AddBehavior Add { get; set; } = (_, _, _, _, _) => Task.FromResult(TestGameData.Added());

    public List<string> Prepared { get; } = [];

    public List<(PreparedGame Game, bool ToIntel)> Adds { get; } = [];

    public Task<PreparedGame> PrepareAsync(string input, CancellationToken cancellationToken)
    {
        Prepared.Add(input);
        return Prepare(input, cancellationToken);
    }

    public Task<GameAdded> AddAsync(PreparedGame game, bool toIntel, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        Adds.Add((game, toIntel));
        return Add(game, toIntel, progress, onEvent, cancellationToken);
    }
}

internal delegate Task<GameReconverted> ReconvertBehavior(string titleId, IProgress<StepProgress>? progress, Action<StepEvent>? onEvent, CancellationToken cancellationToken);

internal sealed class FakeGameReconverter : IGameReconverter
{
    public IReadOnlyList<string> StepNames { get; } =
        ["Set the old conversion aside", "Copy the AnyPS5 libraries", "Unwrap SELF files", "Convert with the relinker", "Audit the imports", "Record the game"];

    public ReconvertBehavior Reconvert { get; set; } = (_, _, _, _) => Task.FromResult(TestGameData.Reconverted());

    public List<string> Reconverted { get; } = [];

    public Task<GameReconverted> ReconvertAsync(string titleId, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        Reconverted.Add(titleId);
        return Reconvert(titleId, progress, onEvent, cancellationToken);
    }
}

/// <summary>Keeps the level and message of each entry.</summary>
internal sealed class ListLogger : Microsoft.Extensions.Logging.ILogger
{
    public List<(Microsoft.Extensions.Logging.LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}

/// <summary>A real library on a temp root with a fake launcher; games are written as an add leaves them (game.json and the executable).</summary>
internal sealed class TestLibrary : IDisposable
{
    public static readonly AnyPS5InstallRecord Release =
        new(1, "test-profile", AnyPS5Mode.Release, "v0.1.2", null, null, new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    private readonly TempDirectory _temp = new();

    public TestLibrary()
    {
        Layout = new DataLayout(_temp.Combine("root"));
        Lock = new PreparationLock(Layout) { RetryDelay = TimeSpan.FromMilliseconds(20) };
        Library = new GameLibrary(Layout, Platform, TimeProvider.System, NullLogger<GameLibrary>.Instance);
        Running = new RunningGames(Launcher, NullLogger<RunningGames>.Instance);
        Installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Release);
        Player = new GamePlayer(Library, Installer, Launcher, Running, new LaunchPreparation(Lavapipe, Fonts, NullLogger<LaunchPreparation>.Instance, Lock), NullLogger<GamePlayer>.Instance);
        Cleaner = new GameCleaner(Library, Running, NullLogger<GameCleaner>.Instance);
    }

    public DataLayout Layout { get; }

    public AnyPS5Paths Paths => new(Layout, Platform);

    public FakeDialogs Dialogs { get; } = new();

    /// <summary>The real diagnoser with the embedded profile's rules, and the real fixes on this library.</summary>
    public DiagnosisServices Diagnosis(Uri? reportUrl = null) => new(
        new CrashDiagnoser(ProfileLoader.LoadEmbedded(), NullLogger<CrashDiagnoser>.Instance),
        new LibsRepair(Library, Installer, Paths, Running),
        new CrashReporter(Library, ProfileLoader.LoadEmbedded(), new FakeCpuInfo(), new FakeVulkanProbeRunner(), Platform),
        Dialogs,
        reportUrl);

    /// <summary>The preparation lock on the test root, retried every 20 ms.</summary>
    public PreparationLock Lock { get; }

    public TestPlatform Platform { get; } = new();

    public FakeGameLauncher Launcher { get; } = new();

    public FakeLavapipe Lavapipe { get; } = new();

    public FakeFontPack Fonts { get; } = new() { Installed = true };

    public FakeInstaller Installer { get; } = new();

    public GameLibrary Library { get; }

    public RunningGames Running { get; }

    public GamePlayer Player { get; }

    public GameCleaner Cleaner { get; }

    public GameLayout Game(string titleId) => new(Layout.GameDirectory(titleId), titleId, Platform);

    public async Task<GameLayout> AddAsync(string titleId, string name, ConversionInfo? convertedWith = null, DateTimeOffset? lastPlayedAt = null)
    {
        var game = Game(titleId);
        Directory.CreateDirectory(game.Root);
        await File.WriteAllTextAsync(game.Executable, "exe");
        await GameRecordStore.WriteAsync(game.RecordFile, new GameRecord
        {
            SchemaVersion = GameRecord.CurrentSchemaVersion,
            TitleId = titleId,
            Name = name,
            Executable = titleId + ".exe",
            ConvertedWith = convertedWith ?? new ConversionInfo("test-profile", "release", "v0.1.2", null, false),
            Audit = new AuditRecord(361, 3, 0, AuditVerdict.Orange),
            AddedAt = DateTimeOffset.UnixEpoch,
            LastPlayedAt = lastPlayedAt,
        }, CancellationToken.None);
        return game;
    }

    public void Dispose()
    {
        // Ends every fake game, so no test leaves a wait running.
        foreach (var game in Launcher.Games.Values)
        {
            game.Exit(0);
        }

        _temp.Dispose();
    }
}
