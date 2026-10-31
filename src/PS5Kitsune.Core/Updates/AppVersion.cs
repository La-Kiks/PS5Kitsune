using System.Globalization;
using System.Reflection;

namespace PS5Kitsune.Core.Updates;

/// <summary>
/// A PS5Kitsune version: major.minor.patch, and whether it is a pre-release (a <c>-</c> part, as CI builds have).
/// The build part after <c>+</c> (the commit) is ignored. Comparison uses major, minor and patch only.
/// </summary>
public readonly record struct AppVersion(int Major, int Minor, int Patch, bool IsPreRelease) : IComparable<AppVersion>
{
    /// <summary>The running app's version, from the entry assembly's informational version; null when it has none that parses.</summary>
    public static AppVersion? Current =>
        Parse(Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "");

    /// <summary>Reads <c>X.Y.Z</c> with an optional leading <c>v</c>, <c>-pre-release</c> and <c>+build</c>; null for anything else.</summary>
    public static AppVersion? Parse(string text)
    {
        var core = text.StartsWith('v') ? text[1..] : text;
        var plus = core.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            core = core[..plus];
        }

        var preRelease = false;
        var dash = core.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            if (dash == 0 || dash == core.Length - 1)
            {
                return null;
            }

            preRelease = true;
            core = core[..dash];
        }

        var parts = core.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var numbers = new int[3];
        for (var i = 0; i < 3; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return null;
            }
        }

        return new AppVersion(numbers[0], numbers[1], numbers[2], preRelease);
    }

    public int CompareTo(AppVersion other) => (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));

    public static bool operator <(AppVersion left, AppVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(AppVersion left, AppVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(AppVersion left, AppVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(AppVersion left, AppVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");
}
