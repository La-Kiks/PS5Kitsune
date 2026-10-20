using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tests.Fakes;

internal sealed record FakeProcessResult(int ExitCode, IReadOnlyList<string>? StandardOutput = null, IReadOnlyList<string>? StandardError = null);

/// <summary>Records every command; <see cref="Respond"/> plays the tool (it may create files) and gives the result.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<ProcessSpec> Calls { get; } = [];

    public ProcessSpec? LastSpec => Calls.LastOrDefault();

    public Func<ProcessSpec, FakeProcessResult> Respond { get; set; } = _ => new FakeProcessResult(0);

    /// <summary>File name of the script for a command built by PythonEmbeddableTool.CreateScriptSpec, else null.</summary>
    public static string? ScriptName(ProcessSpec spec) =>
        spec.Arguments is ["-c", _, var script, ..] ? Path.GetFileName(script) : null;

    public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<OutputLine>? onOutput = null, CancellationToken cancellationToken = default)
    {
        Calls.Add(spec);
        var result = Respond(spec);
        foreach (var line in result.StandardOutput ?? [])
        {
            onOutput?.Invoke(new OutputLine(OutputSource.StandardOutput, line));
        }

        foreach (var line in result.StandardError ?? [])
        {
            onOutput?.Invoke(new OutputLine(OutputSource.StandardError, line));
        }

        return Task.FromResult(new ProcessResult(result.ExitCode, TimeSpan.Zero));
    }
}
