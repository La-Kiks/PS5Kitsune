using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tools;

/// <summary>The Python that runs AnyPS5's scripts: the embeddable zip on Windows, the system python3 on Linux.</summary>
public interface IPythonRuntime
{
    /// <summary>The install step's name: "Install Python", or "Find Python 3" where the system provides it.</summary>
    string StepName { get; }

    /// <summary>True when Python can run the scripts now.</summary>
    bool IsInstalled();

    /// <summary>Downloads Python; where the system provides it, throws <see cref="PythonMissingException"/> naming the command that installs it.</summary>
    Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>A command running <paramref name="script"/> with <paramref name="arguments"/>; the script's exit code is the process exit code.</summary>
    ProcessSpec CreateScriptSpec(string script, IEnumerable<string> arguments);
}
