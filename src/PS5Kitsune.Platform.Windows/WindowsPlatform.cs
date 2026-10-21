using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed partial class WindowsPlatform : IPlatform
{
    public string Name => "Windows";

    /// <summary>
    /// C:\PS5Kitsune rather than %LOCALAPPDATA%: the user name can contain a space,
    /// which breaks WinLibs and CMake.
    /// </summary>
    public string DefaultDataRoot => Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "PS5Kitsune");

    /// <summary>Local, not roaming: the data root it names is a path on this PC.</summary>
    public string SettingsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PS5Kitsune");

    public string ExecutableSuffix => ".exe";

    /// <summary>ullTotalPageFile is the commit limit (RAM + page files), despite its name.</summary>
    public ulong? GetCommitLimitBytes()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return status.TotalPageFile;
    }

    /// <summary>
    /// Manifests registered under HKLM\SOFTWARE\Khronos\Vulkan\Drivers, where Mesa (Dozen, Lavapipe) registers.
    /// GPU vendors' drivers register under their display adapter key instead; the probe sees those.
    /// </summary>
    public IReadOnlyList<string> GetVulkanDriverManifests()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Khronos\Vulkan\Drivers");
        return key?.GetValueNames() ?? [];
    }

    public void OpenFix(CheckFix fix)
    {
        switch (fix)
        {
            case CheckFix.OpenPerformanceSettings:
                using (Process.Start(new ProcessStartInfo("SystemPropertiesPerformance.exe") { UseShellExecute = true }))
                {
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fix), fix, "Unknown fix.");
        }
    }

    public void OpenPath(string path)
    {
        using (Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }))
        {
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

#pragma warning disable CS0649 // Native layout: Windows fills fields the C# code never reads.
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
#pragma warning restore CS0649
}
