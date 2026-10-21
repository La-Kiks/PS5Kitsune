using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.AnyPS5.Build;

/// <summary>Build mode's folders under anyps5\. See the milestone 5a spec, "Layout".</summary>
public sealed class BuildPaths(DataLayout layout, IPlatform platform)
{
    public string Root => layout.AnyPS5;

    /// <summary>The git clone.</summary>
    public string Source => Path.Combine(Root, "source");

    /// <summary>The CMake build folder, outside the clone so a forced checkout never touches it.</summary>
    public string Build => Path.Combine(Root, "build");

    /// <summary>One marker per step, written after the step succeeds.</summary>
    public string State => Path.Combine(Root, "state");

    public string BuiltRelinker => Path.Combine(Build, "core", "relinker", "relinker" + platform.ExecutableSuffix);

    /// <summary>The .prx files of the libs target.</summary>
    public string BuiltLibs => Path.Combine(Build, "core", "libs", "libs");

    public string Marker(string name) => Path.Combine(State, name + ".done");
}
