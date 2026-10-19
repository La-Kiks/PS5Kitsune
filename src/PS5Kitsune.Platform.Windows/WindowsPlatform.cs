using System.Runtime.Versioning;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatform : IPlatform
{
    public string Name => "Windows";

    /// <summary>
    /// C:\PS5Kitsune rather than %LOCALAPPDATA%: the user name can contain a space,
    /// which breaks WinLibs and CMake.
    /// </summary>
    public string DefaultDataRoot => Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "PS5Kitsune");

    public string SettingsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PS5Kitsune");

    public string ExecutableSuffix => ".exe";
}
