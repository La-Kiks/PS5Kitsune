using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Tools;
using PS5Kitsune.Platform.Linux;
using PS5Kitsune.Platform.Windows;

namespace PS5Kitsune.Platform;

public static class PlatformServices
{
    /// <summary>The implementation of <see cref="IPlatform"/> for the running OS.</summary>
    /// <exception cref="PlatformNotSupportedException">Neither Windows nor Linux.</exception>
    public static IPlatform Detect()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatform();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxPlatform(new DistroPackages(OsRelease.Read()));
        }

        throw new PlatformNotSupportedException("PS5Kitsune runs on Windows and Linux.");
    }

    /// <summary>Registers the platform and its services. Call it before AddPS5KitsuneCore, which keeps what is registered here.</summary>
    public static IServiceCollection AddPlatform(this IServiceCollection services, IPlatform platform)
    {
        services.AddSingleton(platform);
        // By the platform's type, not the running OS, so a test can build the Linux services anywhere.
        if (platform is LinuxPlatform linux)
        {
            services.AddSingleton(linux.Packages);
            services.AddSingleton<SystemPython>();
            services.AddSingleton<IPythonRuntime>(provider => provider.GetRequiredService<SystemPython>());
            services.AddSingleton<ILavapipe, SystemLavapipe>();
            services.AddSingleton<ISystemCheck, PythonCheck>();
            services.AddSingleton<ISystemCheck, UdmabufCheck>();
            if (OperatingSystem.IsLinux())
            {
                services.AddSingleton<IGameLauncher, LinuxGameLauncher>();
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IGameLauncher, WindowsGameLauncher>();
            services.AddSingleton<IChildProcessJob, WindowsChildProcessJob>();
        }

        return services;
    }
}
