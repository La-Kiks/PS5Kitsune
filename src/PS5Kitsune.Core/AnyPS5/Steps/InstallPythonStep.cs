using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Steps;

public sealed class InstallPythonStep(PythonEmbeddableTool python) : IStep
{
    public string Name => "Install Python";

    public bool IsAlreadyDone() => python.IsInstalled();

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        python.InstallAsync(StepProgress.ForDownload(progress), cancellationToken);
}
