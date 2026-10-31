using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Platform.Linux.Tests;

[SupportedOSPlatform("linux")]
public sealed class LinuxServicesTests : IDisposable
{
    private readonly PS5Kitsune.Core.Tests.TempDirectory _temp = new();

    private ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlatform(new LinuxPlatform(new DistroPackages(OsRelease.Parse("ID=ubuntu\n"))))
            .AddPS5KitsuneCore(new DataLayout(_temp.Path), ProfileLoader.LoadEmbedded(ProfileOs.Linux));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void The_linux_platform_brings_the_system_python_and_lavapipe()
    {
        using var provider = Build();

        Assert.IsType<SystemPython>(provider.GetRequiredService<IPythonRuntime>());
        Assert.IsType<SystemLavapipe>(provider.GetRequiredService<ILavapipe>());
        Assert.IsType<LinuxPlatform>(provider.GetRequiredService<IPlatform>());
    }

    [Fact]
    public void The_linux_profile_offers_release_mode_only()
    {
        using var provider = Build();
        var installer = provider.GetRequiredService<AnyPS5Installer>();

        Assert.True(installer.Supports(AnyPS5Mode.Release));
        Assert.False(installer.Supports(AnyPS5Mode.Build));
        Assert.Equal("Find Python 3", installer.CreateSteps(AnyPS5Mode.Release)[0].Name);
    }

    [Fact]
    public void The_linux_platform_brings_its_game_launcher()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var provider = Build();

        Assert.IsType<LinuxGameLauncher>(provider.GetRequiredService<IGameLauncher>());
    }

    [Fact]
    public void The_linux_platform_adds_the_python_and_udmabuf_checks()
    {
        using var provider = Build();

        var ids = provider.GetServices<PS5Kitsune.Core.Checks.ISystemCheck>().Select(check => check.Id).ToList();

        Assert.Contains("python", ids);
        Assert.Contains("udmabuf", ids);
    }

    public void Dispose() => _temp.Dispose();
}
