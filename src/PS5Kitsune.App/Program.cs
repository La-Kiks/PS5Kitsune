using Avalonia;
using PS5Kitsune.Core.Checks.Vulkan;

namespace PS5Kitsune.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // The Vulkan probe child (see VulkanProbeRunner): nothing else is loaded, so a crashing driver only takes this process down.
        if (args is [VulkanProbeRunner.ProbeArgument])
        {
            Console.Out.Write(VulkanProbe.Run().ToJson());
            return 0;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the Avalonia designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
