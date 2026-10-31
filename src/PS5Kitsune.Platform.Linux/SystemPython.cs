using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Platform.Linux;

/// <summary>The distribution's python3, found on PATH. Nothing is downloaded: a missing Python stops with the command that installs it.</summary>
public sealed class SystemPython(DistroPackages packages) : IPythonRuntime
{
    /// <summary>The PATH searched: the process's own.</summary>
    internal string? SearchPath { get; init; } = Environment.GetEnvironmentVariable("PATH");

    public string StepName => "Find Python 3";

    /// <summary>The full path of python3; null when it is not on PATH.</summary>
    public string? Executable => PathSearch.Find("python3", SearchPath);

    public bool IsInstalled() => Executable is not null;

    public Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) =>
        IsInstalled() ? Task.CompletedTask : Task.FromException(new PythonMissingException(MissingMessage));

    public ProcessSpec CreateScriptSpec(string script, IEnumerable<string> arguments) =>
        PythonScripts.CreateSpec(Executable ?? throw new PythonMissingException(MissingMessage), script, arguments);

    private string MissingMessage => $"Python 3 is required: {packages.CommandFor(LinuxPackage.Python)}";
}
