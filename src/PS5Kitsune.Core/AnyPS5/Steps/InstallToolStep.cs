using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Steps;

public sealed class InstallToolStep(string name, IPortableTool tool) : IStep
{
    public string Name => name;

    public bool IsAlreadyDone() => tool.IsInstalled();

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        tool.InstallAsync(StepProgress.ForDownload(progress), cancellationToken);
}
