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

        // CliWrap's cancellation may stop only the direct child; Ninja's g++ and cc1plus would keep running.
        // So the command runs without CliWrap's token, and cancellation kills the whole tree.
        var task = command.ExecuteAsync(CancellationToken.None);
        CommandResult result;
        using (cancellationToken.Register(() => KillTree(spec.FileName, task.ProcessId)))
        {
            result = await task.ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
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
            if (source == OutputSource.StandardError && !spec.Quiet)
            {
                LogErrorOutput(spec.FileName, text);
            }
            else
            {
                LogOutput(spec.FileName, text);
            }

            onOutput?.Invoke(new OutputLine(source, text));
        }
    }

    private void KillTree(string fileName, int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // Already exited, so there is nothing left to stop.
        }
        catch (Exception exception) when (exception is AggregateException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            // Runs inside a cancellation callback, so it must not throw: log and keep going.
            LogKillFailed(fileName, exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not stop every process started by {FileName}: {Reason}")]
    private partial void LogKillFailed(string fileName, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{FileName}] {Text}")]
    private partial void LogErrorOutput(string fileName, string text);

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
