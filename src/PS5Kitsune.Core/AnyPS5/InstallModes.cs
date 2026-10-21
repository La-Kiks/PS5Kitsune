namespace PS5Kitsune.Core.AnyPS5;

/// <param name="Source">Where the mode came from, shown on the "Mode:" line: "--mode", "unfinished install", "current install" or "default".</param>
public sealed record InstallModeChoice(AnyPS5Mode Mode, string Source);

public static class InstallModes
{
    /// <summary>
    /// The --mode option, else the mode of an install that did not finish, else the mode of the current install, else
    /// release; so a plain 'install' resumes an interrupted install and updates a build.
    /// </summary>
    public static InstallModeChoice Resolve(AnyPS5Mode? option, AnyPS5Mode? unfinished, AnyPS5InstallRecord? record) =>
        option is { } chosen ? new InstallModeChoice(chosen, "--mode")
        : unfinished is { } requested ? new InstallModeChoice(requested, "unfinished install")
        : record is not null ? new InstallModeChoice(record.Mode, "current install")
        : new InstallModeChoice(AnyPS5Mode.Release, "default");

    public static string Name(AnyPS5Mode mode) => mode switch
    {
        AnyPS5Mode.Release => "release",
        AnyPS5Mode.Build => "build",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
