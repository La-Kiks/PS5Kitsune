using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tests.Fakes;

/// <summary>Launches fake games that run until a test ends them; also linked into the App tests.</summary>
internal sealed class FakeGameLauncher : IGameLauncher
{
    private int _nextProcessId = 4812;

    public List<GameLaunchRequest> Launches { get; } = [];

    /// <summary>Every game launched, by PID; an exited one stays listed.</summary>
    public Dictionary<int, FakeLaunchedGame> Games { get; } = [];

    /// <summary>Thrown by Launch when set.</summary>
    public Exception? LaunchFailure { get; set; }

    public DateTimeOffset Now { get; set; } = new(2026, 10, 24, 18, 3, 11, TimeSpan.Zero);

    public ILaunchedGame Launch(GameLaunchRequest request)
    {
        if (LaunchFailure is not null)
        {
            throw LaunchFailure;
        }

        Launches.Add(request);
        Directory.CreateDirectory(request.LogDirectory);
        var logFile = Path.Combine(request.LogDirectory, $"{Launches.Count:000}.log");
        File.WriteAllText(logFile, "");
        var game = new FakeLaunchedGame(_nextProcessId++, logFile, Now);
        Games[game.ProcessId] = game;
        return game;
    }

    public ILaunchedGame? Attach(RunningGame record) =>
        Games.TryGetValue(record.ProcessId, out var game) && game.StartedAt == record.StartedAt && !game.HasExited ? game : null;
}

internal sealed class FakeLaunchedGame(int processId, string logFile, DateTimeOffset startedAt) : ILaunchedGame
{
    private readonly TaskCompletionSource<int?> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int ProcessId { get; } = processId;

    public string LogFile { get; } = logFile;

    public DateTimeOffset StartedAt { get; } = startedAt;

    public bool HasExited => _exit.Task.IsCompleted;

    /// <summary>Thrown by StopAsync when set; the game then keeps running.</summary>
    public Exception? StopFailure { get; set; }

    public void Exit(int? exitCode) => _exit.TrySetResult(exitCode);

    public Task<int?> WaitForExitAsync(CancellationToken cancellationToken = default) => _exit.Task.WaitAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (StopFailure is not null)
        {
            throw StopFailure;
        }

        Exit(1);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
