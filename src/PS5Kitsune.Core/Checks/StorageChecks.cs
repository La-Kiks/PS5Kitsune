using System.Globalization;
using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.Core.Checks;

public sealed class DiskSpaceCheck : SystemCheck
{
    private const double GiB = 1L << 30;

    public override string Id => "disk-space";

    public override string Title => "Free disk space";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.FreeDiskBytes is not { } bytes)
        {
            return Result(CheckStatus.Info, "Could not read the free space of the data folder's drive.");
        }

        var free = bytes / GiB;
        var shown = Math.Floor(free * 10) / 10;
        var build = facts.Mode == AnyPS5Mode.Build;
        var needed = build ? facts.Limits.BuildMinFreeDiskGiB : facts.Limits.MinFreeDiskGiB;
        var what = build ? "AnyPS5 built from source and a game" : "AnyPS5 and a game";
        return free >= needed
            ? Result(CheckStatus.Ok, string.Create(CultureInfo.InvariantCulture, $"{shown:0.0} GiB free on the data folder's drive."))
            : Result(
                CheckStatus.Warning,
                string.Create(CultureInfo.InvariantCulture, $"{shown:0.0} GiB free on the data folder's drive; {what} need about {needed} GiB."),
                "Free some space, or choose a data folder on another drive.");
    }
}

public sealed class InstallPathCheck : SystemCheck
{
    public override string Id => "install-path";

    public override string Title => "Data folder path";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var root = facts.DataRoot;
        return root.All(character => char.IsAscii(character) && character != ' ')
            ? Result(CheckStatus.Ok, root)
            : Result(
                CheckStatus.Blocking,
                $"{root} contains spaces or non-ASCII characters; AnyPS5's tools fail on such paths.",
                $"Choose a data folder without spaces or accents, for example {facts.DefaultDataRoot} (command line: --root).");
    }
}
