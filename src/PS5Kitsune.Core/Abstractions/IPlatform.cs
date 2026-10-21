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

    /// <summary>Memory the OS can commit (RAM + page file), in bytes; null where the OS has no such limit.</summary>
    ulong? GetCommitLimitBytes();

    /// <summary>Registered Vulkan driver manifests (ICD JSON paths); empty when none are registered this way.</summary>
    IReadOnlyList<string> GetVulkanDriverManifests();

    /// <summary>Opens what the user needs to apply a check's fix, for example a settings dialog.</summary>
    void OpenFix(CheckFix fix);
}
