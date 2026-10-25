using System.Globalization;
using PS5Kitsune.App.Controls;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.App.ViewModels;

/// <summary>One tool on the Tools screen.</summary>
public sealed class ToolRowViewModel(PortableToolInfo info)
{
    public PortableToolInfo Info => info;

    public string Name => info.DisplayName;

    public string Version => info.Version;

    public bool IsInstalled => info.IsInstalled;

    public StatusKind Kind => info.IsInstalled ? StatusKind.Green : StatusKind.Neutral;

    public string StatusText => info.IsInstalled ? "Installed" : "Missing";

    /// <summary>Empty for an installed tool.</summary>
    public string SizeText => info.IsInstalled ? ""
        : info.SizeBytes is { } size ? FormatMegabytes(size)
        : "size unknown";

    internal static string FormatMegabytes(long bytes) => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1048576.0:0.0} MB");
}
