using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.Core.Library;

/// <summary>"{mode} {version}", e.g. "build 3fd1cc3" or "release v0.1.2", for a game's conversion and for an install.</summary>
public static class ConversionLabels
{
    public static string Of(ConversionInfo conversion)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        return $"{conversion.Mode} {conversion.Version}";
    }

    public static string Of(AnyPS5InstallRecord install)
    {
        ArgumentNullException.ThrowIfNull(install);
        return $"{InstallModes.Name(install.Mode)} {install.Version}";
    }
}
