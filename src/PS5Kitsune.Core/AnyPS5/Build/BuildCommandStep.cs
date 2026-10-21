using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5.Build;

/// <summary>A build step done when its marker holds the profile's commit. The marker is removed first and written after success.</summary>
public abstract class BuildCommandStep(BuildMarkers markers, string marker) : IStep
{
    public abstract string Name { get; }

    public bool IsAlreadyDone() => markers.IsCurrent(marker);

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        markers.Clear(marker);
        await RunCommandsAsync(progress, cancellationToken).ConfigureAwait(false);
        await markers.WriteAsync(marker, cancellationToken).ConfigureAwait(false);
    }

    protected abstract Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken);
}
