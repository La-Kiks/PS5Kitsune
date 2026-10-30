using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Processes;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests;
using PS5Kitsune.Platform;

namespace PS5Kitsune.Platform.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class PlatformServicesTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void The_platform_registers_the_job_and_the_game_launcher_the_core_keeps()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlatform(new WindowsPlatform()).AddPS5KitsuneCore(new DataLayout(_temp.Path), ProfileLoader.LoadEmbedded());
        using var provider = services.BuildServiceProvider();

        Assert.IsType<WindowsChildProcessJob>(provider.GetRequiredService<IChildProcessJob>());
        Assert.IsType<WindowsGameLauncher>(provider.GetRequiredService<IGameLauncher>());
    }

    public void Dispose() => _temp.Dispose();
}
