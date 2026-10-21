using System.Diagnostics;
using CliWrap;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Processes;

public sealed partial class CliWrapProcessRunner(ILogger<CliWrapProcessRunner> logger) : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessSpec spec, Action<OutputLine>? onOutput = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var command = Cli.Wrap(spec.FileName)
            .WithArguments(spec.Arguments)
            .WithEnvironmentVariables(spec.Environment)
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Forward(OutputSource.StandardOutput, line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Forward(OutputSource.StandardError, line)));
        if (spec.WorkingDirectory is not null)
        {
            command = command.WithWorkingDirectory(spec.WorkingDirectory);
        }

        if (spec.Quiet)
        {
            LogStartingQuietly(spec.FileName, spec.Arguments);
        }
        else
        {
            LogStarting(spec.FileName, spec.Arguments);
        }

        var stopwatch = Stopwatch.StartNew();
        var result = await command.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        if (spec.Quiet)
        {
            LogFinishedQuietly(spec.FileName, result.ExitCode, stopwatch.Elapsed);
        }
        else
        {
            LogFinished(spec.FileName, result.ExitCode, stopwatch.Elapsed);
        }

        return new ProcessResult(result.ExitCode, stopwatch.Elapsed);

        void Forward(OutputSource source, string text)
        {
            LogOutput(spec.FileName, text);
            onOutput?.Invoke(new OutputLine(source, text));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Running {FileName} {Arguments}")]
    private partial void LogStarting(string fileName, IReadOnlyList<string> arguments);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[{FileName}] {Text}")]
    private partial void LogOutput(string fileName, string text);

    [LoggerMessage(Level = LogLevel.Information, Message = "{FileName} exited with code {ExitCode} after {Duration}")]
    private partial void LogFinished(string fileName, int exitCode, TimeSpan duration);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Running {FileName} {Arguments}")]
    private partial void LogStartingQuietly(string fileName, IReadOnlyList<string> arguments);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{FileName} exited with code {ExitCode} after {Duration}")]
    private partial void LogFinishedQuietly(string fileName, int exitCode, TimeSpan duration);
}
