using System.Globalization;
using System.Text.RegularExpressions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Processes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Build;

/// <param name="ExitCode">The process exit code.</param>
/// <param name="Tail">The last lines of output, Ninja counters excluded.</param>
/// <param name="SawCertificateError">An HTTPS certificate error appeared in the error output (curl error 60 and friends).</param>
public sealed record CommandFailure(int ExitCode, string Tail, bool SawCertificateError);

public sealed class BuildCommandException(string message) : Exception(message);

/// <summary>Runs git and CMake for build mode with the build environment, reports progress, and turns failures into messages.</summary>
public sealed partial class BuildCommands(IProcessRunner runner, BuildEnvironment environment, BuildPaths paths, IFreeSpace freeSpace)
{
    private const long LowDiskBytes = 1L << 30;

    public ProcessSpec Git(params string[] arguments) =>
        new(environment.Git, arguments) { WorkingDirectory = paths.Source, Environment = environment.Variables() };

    public ProcessSpec CMake(params string[] arguments) =>
        new(environment.CMake, arguments) { WorkingDirectory = paths.Root, Environment = environment.Variables() };

    /// <exception cref="BuildCommandException">The command exited with a non-zero code.</exception>
    public async Task RunAsync(ProcessSpec spec, Func<CommandFailure, string> describeFailure, IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(describeFailure);
        var tail = new OutputTail();
        var sawCertificateError = false;
        var result = await runner.RunAsync(spec, line =>
        {
            if (NinjaProgress.TryParse(line.Text, out var fraction))
            {
                progress?.Report(new StepProgress(Fraction: fraction));
                return;
            }

            tail.Add(line.Text);
            if (line.Source == OutputSource.StandardError && CertificateErrorPattern().IsMatch(line.Text))
            {
                sawCertificateError = true;
            }

            if (ErrorLinePattern().IsMatch(line.Text))
            {
                progress?.Report(new StepProgress(Message: line.Text));
            }
        }, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            return;
        }

        var lines = tail.ToString();
        var message = LowDiskMessage() ?? describeFailure(new CommandFailure(result.ExitCode, lines, sawCertificateError));
        throw new BuildCommandException(
            $"{message}{Environment.NewLine}{Path.GetFileNameWithoutExtension(spec.FileName)} exited with code {result.ExitCode}.{Environment.NewLine}{lines}");
    }

    private string? LowDiskMessage()
    {
        if (freeSpace.FreeBytes(paths.Root) is not { } free || free >= LowDiskBytes)
        {
            return null;
        }

        var drive = Path.GetPathRoot(Path.GetFullPath(paths.Root));
        return string.Create(CultureInfo.InvariantCulture, $"Not enough disk space on {drive}: {free / (double)(1L << 30):0.0} GiB left.");
    }

    [GeneratedRegex(@"status_code: (35|60|77)\b|SSL peer certificate|SSL certificate problem|certificate verify failed", RegexOptions.IgnoreCase)]
    private static partial Regex CertificateErrorPattern();

    [GeneratedRegex(@"\berror:|\bfatal:|CMake Error|^FAILED: ")]
    private static partial Regex ErrorLinePattern();
}
