using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Processes;

namespace PS5Kitsune.Core.Tests.Processes;

/// <summary>Runs the dotnet host, which exists on every machine that runs these tests.</summary>
public sealed class CliWrapProcessRunnerTests
{
    private readonly CliWrapProcessRunner _runner = new(NullLogger<CliWrapProcessRunner>.Instance);

    [Fact]
    public async Task Streams_standard_output_and_returns_the_exit_code()
    {
        var lines = new List<OutputLine>();

        var result = await _runner.RunAsync(new ProcessSpec("dotnet", ["--version"]), lines.Add, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(lines, line => line.Source == OutputSource.StandardOutput && line.Text.StartsWith("10.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Returns_a_non_zero_exit_code_instead_of_throwing()
    {
        var result = await _runner.RunAsync(new ProcessSpec("dotnet", ["no-such-command-ps5kitsune"]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task Passes_environment_variables_to_the_child_only()
    {
        var spec = new ProcessSpec("dotnet", ["--info"])
        {
            Environment = new Dictionary<string, string?> { ["PS5KITSUNE_TEST_VARIABLE"] = "1" },
        };

        var result = await _runner.RunAsync(spec, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Null(Environment.GetEnvironmentVariable("PS5KITSUNE_TEST_VARIABLE"));
    }

    [Theory]
    [InlineData(false, LogLevel.Information)]
    [InlineData(true, LogLevel.Debug)]
    public async Task Logs_the_start_and_the_exit_at_Information_unless_the_spec_is_quiet(bool quiet, LogLevel expected)
    {
        var logger = new CapturingLogger();
        var runner = new CliWrapProcessRunner(logger);

        await runner.RunAsync(new ProcessSpec("dotnet", ["--version"]) { Quiet = quiet }, cancellationToken: TestContext.Current.CancellationToken);

        var lifecycle = logger.Entries.Where(entry => entry.Message.StartsWith("Running ", StringComparison.Ordinal) || entry.Message.Contains(" exited with code ", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, lifecycle.Count);
        Assert.All(lifecycle, entry => Assert.Equal(expected, entry.Level));
    }

    private sealed class CapturingLogger : ILogger<CliWrapProcessRunner>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
