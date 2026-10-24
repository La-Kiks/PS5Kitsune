using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Core.Tests.Fakes;

/// <summary>Mesa without a download: installing reports <see cref="Reports"/>, waits for <see cref="Gate"/> when set, then fails or installs. Also linked into the App tests.</summary>
internal sealed class FakeLavapipe : ILavapipe
{
    public const long Total = 70L << 20;

    public string Manifest { get; set; } = @"C:\root\tools\mesa\lvp_icd.x86_64.json";

    public bool Installed { get; set; }

    public Exception? Failure { get; set; }

    public int Installs { get; private set; }

    /// <summary>Bytes received, reported in order with <see cref="Total"/>.</summary>
    public long[] Reports { get; set; } = [];

    public TaskCompletionSource? Gate { get; set; }

    public bool IsInstalled() => Installed;

    public async Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Installs++;
        foreach (var received in Reports)
        {
            progress?.Report(new DownloadProgress(received, Total));
        }

        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        Installed = true;
    }
}

/// <summary>The fonts without a download; same behavior as <see cref="FakeLavapipe"/>.</summary>
internal sealed class FakeFontPack : IFontPack
{
    public const long Total = 86L << 20;

    public string Folder { get; set; } = @"C:\root\fonts";

    public bool Installed { get; set; }

    public Exception? Failure { get; set; }

    public int Installs { get; private set; }

    public long[] Reports { get; set; } = [];

    public TaskCompletionSource? Gate { get; set; }

    public bool IsInstalled() => Installed;

    public async Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Installs++;
        foreach (var received in Reports)
        {
            progress?.Report(new DownloadProgress(received, Total));
        }

        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        Installed = true;
    }
}
