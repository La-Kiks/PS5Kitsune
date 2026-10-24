using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Platform.Windows;

public static class PlatformServices
{
    /// <summary>The implementation of <see cref="IPlatform"/> for the running OS.</summary>
    /// <exception cref="PlatformNotSupportedException">Not running on Windows (Linux arrives at milestone 8).</exception>
    public static IPlatform Detect()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatform();
        }

        throw new PlatformNotSupportedException("PS5Kitsune currently runs on Windows only; Linux support is planned.");
    }

    public static IServiceCollection AddPlatform(this IServiceCollection services, IPlatform platform)
    {
        services.AddSingleton(platform);
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IGameLauncher, WindowsGameLauncher>();
            services.AddSingleton<IChildProcessJob, WindowsChildProcessJob>();
        }

        return services;
    }
}
