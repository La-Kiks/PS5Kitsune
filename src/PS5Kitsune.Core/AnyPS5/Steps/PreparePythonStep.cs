using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Steps;

/// <summary>Makes sure Python can run AnyPS5's scripts: downloads the embeddable zip, or finds the system python3.</summary>
public sealed class PreparePythonStep(IPythonRuntime python) : IStep
{
    public string Name => python.StepName;

    public bool IsAlreadyDone() => python.IsInstalled();

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        python.InstallAsync(StepProgress.ForDownload(progress), cancellationToken);
}
