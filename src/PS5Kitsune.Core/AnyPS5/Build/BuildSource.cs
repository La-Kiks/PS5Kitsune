using PS5Kitsune.Core.AnyPS5.Steps;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Build;

/// <summary>Build mode: MinGit and WinLibs, the profile's commit, configure, compile, libs, then a copy into anyps5\current.</summary>
public sealed class BuildSource(
    AnyPS5Profile profile,
    MinGitTool mingit,
    WinLibsTool winlibs,
    BuildEnvironment environment,
    BuildPaths paths,
    BuildMarkers markers,
    BuildCommands commands,
    AnyPS5Paths current) : IAnyPS5Source
{
    private const int ShortCommitLength = 7;

    public AnyPS5Mode Mode => AnyPS5Mode.Build;

    public IReadOnlyList<IStep> CreateSteps()
    {
        var build = profile.RequireBuild();
        var jobs = ParallelJobs.ForThisMachine();
        return
        [
            new InstallToolStep("Install MinGit", mingit),
            new InstallToolStep("Install WinLibs", winlibs),
            new GetSourceStep(commands, markers, paths, build),
            new SubmodulesStep(commands, markers),
            new ConfigureStep(commands, markers, paths, environment),
            new CompileStep(commands, markers, paths, jobs),
            new LibsStep(commands, markers, paths, jobs),
            new CopyBuildStep(paths, current, winlibs),
        ];
    }

    public AnyPS5InstallRecord CreateRecord(DateTimeOffset installedAt)
    {
        var commit = profile.RequireBuild().Commit;
        return new AnyPS5InstallRecord(
            AnyPS5InstallRecord.CurrentSchemaVersion,
            profile.Id,
            AnyPS5Mode.Build,
            commit[..ShortCommitLength],
            commit,
            UnwrapScript: null,
            installedAt);
    }
}
