using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;

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
    public AnyPS5InstallState State { get; set; } = new(AnyPS5InstallStatus.NotInstalled, null);

    public InstallBehavior Install { get; set; } = (_, _, _) => Task.CompletedTask;

    public int Runs { get; private set; }

    public AnyPS5Mode CurrentMode { get; set; } = AnyPS5Mode.Release;

    public AnyPS5Mode? RequestedMode { get; set; }

    public AnyPS5Mode? InstalledMode { get; private set; }

    public AnyPS5InstallRecord? ReadRecord() => State.Record;

    public IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode) => [.. stepNames.Select(name => new NamedStep(name))];

    public AnyPS5InstallState GetState(AnyPS5Mode mode) => State;

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

/// <summary>Pages are plain strings, so a test asserts where the navigator went.</summary>
internal sealed class FakePages : IPages
{
    public object Home() => "home";

    public object SystemCheck() => "check";

    public object Mode() => "mode";

    public object Install() => "install";

    public object Done() => "done";
}

internal sealed class FakeDialogs : IDialogService
{
    public string? Folder { get; set; }

    public bool Confirm { get; set; }

    public List<string> Confirmations { get; } = [];

    public Task<string?> PickFolderAsync(string title, string? startFolder) => Task.FromResult(Folder);

    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Confirmations.Add(message);
        return Task.FromResult(Confirm);
    }
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
        return new RootSession(new DataLayout(root), services.BuildServiceProvider(), Path.Combine(root, "logs", "app.log"));
    }
}
