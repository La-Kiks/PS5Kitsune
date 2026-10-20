using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Downloads;
using PS5Kitsune.Core.Games;
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
        services.AddSingleton<IProcessRunner, CliWrapProcessRunner>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<StepRunner>();
        services.AddSingleton<AnyPS5Paths>();
        services.AddSingleton<AnyPS5Installer>();
        services.AddSingleton<GameSourceResolver>();
        services.AddSingleton(_ => new GameValidator());
        services.AddSingleton<GameImporter>();

        if (profile.Tools.TryGetValue(PythonEmbeddableTool.ToolId, out var python))
        {
            services.AddSingleton(provider => ActivatorUtilities.CreateInstance<PythonEmbeddableTool>(provider, python));
            services.AddSingleton<IPortableTool>(provider => provider.GetRequiredService<PythonEmbeddableTool>());
        }

        return services;
    }
}
