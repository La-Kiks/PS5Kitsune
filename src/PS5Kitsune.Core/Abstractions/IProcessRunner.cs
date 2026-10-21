namespace PS5Kitsune.Core.Abstractions;

public enum OutputSource
{
    StandardOutput,
    StandardError,
}

public readonly record struct OutputLine(OutputSource Source, string Text);

/// <summary>A command to run. The environment only applies to the child process.</summary>
public sealed record ProcessSpec(string FileName, IReadOnlyList<string> Arguments)
{
    public string? WorkingDirectory { get; init; }

    /// <summary>Logs start and exit at Debug: for internal helpers such as the Vulkan probe.</summary>
    public bool Quiet { get; init; }

    /// <summary>Variables set (or removed, when null) for the child process only.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();
}

public sealed record ProcessResult(int ExitCode, TimeSpan Duration);

/// <summary>Runs tools (git, cmake, python, relinker) and streams their output line by line.</summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs the process to completion. A non-zero exit code is returned, not thrown:
    /// callers decide what each code means.
    /// </summary>
    Task<ProcessResult> RunAsync(ProcessSpec spec, Action<OutputLine>? onOutput = null, CancellationToken cancellationToken = default);
}
