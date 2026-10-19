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
}
