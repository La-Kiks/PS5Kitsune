using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Core.Abstractions;

/// <summary>
/// Facts that differ between operating systems. Only the Platform projects implement it.
/// </summary>
public interface IPlatform
{
    /// <summary>Short name shown in logs, for example "Windows".</summary>
    string Name { get; }

    /// <summary>Default data root (tools, AnyPS5, games). Must contain no spaces.</summary>
    string DefaultDataRoot { get; }

    /// <summary>Folder holding the app's settings file.</summary>
    string SettingsDirectory { get; }

    /// <summary>Suffix of native executables: ".exe" on Windows, empty on Linux.</summary>
    string ExecutableSuffix { get; }

    /// <summary>The <see cref="Profiles.ProfileOs"/> value of this OS: picks the embedded profile and checks an override's "os".</summary>
    string Os { get; }

    /// <summary>Suffix of a converted game: ".exe" on Windows, ".elf" on Linux.</summary>
    string GameExecutableSuffix { get; }

    /// <summary>The Vulkan loader's file name, for the Vulkan check: "vulkan-1.dll", "libvulkan.so.1".</summary>
    string VulkanLoaderName { get; }

    /// <summary>What to do when Vulkan is missing or broken, shown by the Vulkan check.</summary>
    string VulkanInstallAction { get; }

    /// <summary>Memory the OS can commit (RAM + page file), in bytes; null where the OS has no such limit.</summary>
    ulong? GetCommitLimitBytes();

    /// <summary>Registered Vulkan driver manifests (ICD JSON paths); empty when none are registered this way.</summary>
    IReadOnlyList<string> GetVulkanDriverManifests();

    /// <summary>Opens what the user needs to apply a check's fix, for example a settings dialog.</summary>
    void OpenFix(CheckFix fix);

    /// <summary>Opens a folder in the file manager, a file with its default app, or a web page (an https URL) in the default browser.</summary>
    void OpenPath(string path);
}
