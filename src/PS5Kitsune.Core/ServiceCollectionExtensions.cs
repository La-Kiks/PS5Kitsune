using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.AnyPS5.Build;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Downloads;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Processes;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OS-independent services. The host registers <see cref="IPlatform"/> and logging.
    /// </summary>
    public static IServiceCollection AddPS5KitsuneCore(this IServiceCollection services, DataLayout layout, AnyPS5Profile profile)
    {
        services.AddSingleton(layout);
        services.AddSingleton(profile);
        services.AddSingleton(_ =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"PS5Kitsune/{typeof(ServiceCollectionExtensions).Assembly.GetName().Version}");
            return client;
        });
        services.AddSingleton<IDownloader, HttpDownloader>();
        services.TryAddSingleton<IChildProcessJob, NoChildProcessJob>();
        services.AddSingleton<IProcessRunner, CliWrapProcessRunner>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICpuInfo, X86CpuInfo>();
        services.AddSingleton<IVulkanProbeRunner, VulkanProbeRunner>();
        services.AddSingleton<SystemFactsCollector>();
        services.AddSingleton<ISystemChecker, SystemChecker>();
        foreach (var check in SystemChecks.All)
        {
            // Registered against the interface: SystemChecker receives them as IEnumerable<ISystemCheck>, in this order.
            services.AddSingleton<ISystemCheck>(check);
        }

        services.AddSingleton<StepRunner>();
        services.AddSingleton<AnyPS5Paths>();
        services.AddSingleton<IAnyPS5Source, ReleaseSource>();
        if (profile.Build is not null && profile.Tools.ContainsKey(MinGitTool.ToolId) && profile.Tools.ContainsKey(WinLibsTool.ToolId))
        {
            services.AddSingleton<IAnyPS5Source, BuildSource>();
        }
        services.AddSingleton<AnyPS5Installer>();
        services.AddSingleton<IAnyPS5Installer>(provider => provider.GetRequiredService<AnyPS5Installer>());
        services.AddSingleton<GameSourceResolver>();
        services.AddSingleton(_ => new GameValidator());
        services.AddSingleton<GameImporter>();
        services.AddSingleton<IGameImporter>(provider => provider.GetRequiredService<GameImporter>());
        services.AddSingleton<GameLibrary>();
        // The platform registers its launcher first; elsewhere nothing can run, so nothing is running.
        services.TryAddSingleton<IGameLauncher, UnsupportedGameLauncher>();
        services.AddSingleton<RunningGames>();
        services.AddSingleton(provider => new LaunchPreparation(
            provider.GetService<ILavapipe>(), provider.GetService<IFontPack>(), provider.GetRequiredService<ILogger<LaunchPreparation>>()));
        services.AddSingleton<GamePlayer>();
        services.AddSingleton<GameCleaner>();
        services.AddSingleton<GameReconverter>();
        services.AddSingleton<IGameReconverter>(provider => provider.GetRequiredService<GameReconverter>());

        AddTool<PythonEmbeddableTool>(services, profile, PythonEmbeddableTool.ToolId);
        AddTool<MinGitTool>(services, profile, MinGitTool.ToolId);
        AddTool<WinLibsTool>(services, profile, WinLibsTool.ToolId);
        AddTool<MesaTool>(services, profile, MesaTool.ToolId);
        if (profile.Tools.ContainsKey(MesaTool.ToolId))
        {
            services.AddSingleton<ILavapipe>(provider => provider.GetRequiredService<MesaTool>());
        }

        if (profile.Fonts.Count > 0)
        {
            services.AddSingleton<FontPack>();
            services.AddSingleton<IFontPack>(provider => provider.GetRequiredService<FontPack>());
        }

        services.AddSingleton<BuildToolsSummary>();
        services.AddSingleton<BuildEnvironment>();
        services.AddSingleton<IFreeSpace, DriveFreeSpace>();
        services.AddSingleton<BuildPaths>();
        services.AddSingleton<BuildMarkers>();
        services.AddSingleton<BuildCommands>();

        return services;
    }

    /// <summary>Registers a tool the profile declares, as itself and as <see cref="IPortableTool"/>, so 'tools list' shows it.</summary>
    private static void AddTool<TTool>(IServiceCollection services, AnyPS5Profile profile, string id)
        where TTool : class, IPortableTool
    {
        if (profile.Tools.TryGetValue(id, out var definition))
        {
            services.AddSingleton(provider => ActivatorUtilities.CreateInstance<TTool>(provider, definition));
            services.AddSingleton<IPortableTool>(provider => provider.GetRequiredService<TTool>());
        }
    }
}
