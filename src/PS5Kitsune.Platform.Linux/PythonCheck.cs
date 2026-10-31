using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Platform.Linux;

/// <summary>AnyPS5's scripts need the system python3 (the import audit, unwrapping).</summary>
public sealed class PythonCheck(SystemPython python, DistroPackages packages) : SystemCheck
{
    public override string Id => "python";

    public override string Title => "Python 3";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return python.Executable is { } path
            ? Result(CheckStatus.Ok, $"{path}.")
            : Result(CheckStatus.Blocking, "python3 was not found.", packages.CommandFor(LinuxPackage.Python));
    }
}
