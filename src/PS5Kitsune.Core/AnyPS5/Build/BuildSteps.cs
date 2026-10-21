using System.Globalization;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Build;

// CMake reads backslashes in -D values as escapes: it gets forward slashes.
file static class CMakePaths
{
    public static string Of(string path) => path.Replace('\\', '/');
}

/// <summary>
/// Fetches only the profile's commit (depth 1, by SHA, which GitHub allows) and checks it out.
/// Every command is idempotent, so a run interrupted anywhere resumes cleanly.
/// </summary>
public sealed class GetSourceStep(BuildCommands commands, BuildMarkers markers, BuildPaths paths, ProfileBuild build)
    : BuildCommandStep(markers, "checkout")
{
    private const string NetworkMessage = "Cannot download from GitHub: check the connection, then run install again.";

    public override string Name => "Get the AnyPS5 source";

    protected override async Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.Source);
        var prepare = $"Git could not prepare {paths.Source}.";
        await commands.RunAsync(commands.Git("init"), _ => prepare, progress, cancellationToken).ConfigureAwait(false);
        await commands.RunAsync(commands.Git("config", "core.longpaths", "true"), _ => prepare, progress, cancellationToken).ConfigureAwait(false);
        await commands.RunAsync(commands.Git("config", "remote.origin.url", build.Repository.ToString()), _ => prepare, progress, cancellationToken).ConfigureAwait(false);
        await commands.RunAsync(commands.Git("fetch", "--depth", "1", "origin", build.Commit), _ => NetworkMessage, progress, cancellationToken).ConfigureAwait(false);
        await commands.RunAsync(commands.Git("checkout", "--detach", "--force", build.Commit), _ => $"Git could not check out {build.Commit}.", progress, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Submodules at depth 1, as AnyPS5's CI gets them through actions/checkout.</summary>
public sealed class SubmodulesStep(BuildCommands commands, BuildMarkers markers) : BuildCommandStep(markers, "submodules")
{
    private const string NetworkMessage = "Cannot download from GitHub: check the connection, then run install again.";

    public override string Name => "Update the submodules";

    protected override async Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        await commands.RunAsync(commands.Git("submodule", "sync", "--recursive"), _ => NetworkMessage, progress, cancellationToken).ConfigureAwait(false);
        await commands.RunAsync(commands.Git("submodule", "update", "--init", "--recursive", "--depth", "1", "--force"), _ => NetworkMessage, progress, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Configures with Ninja and the WinLibs compilers. BUILD_TESTING stays OFF, so the libs target is built separately.</summary>
public sealed class ConfigureStep(BuildCommands commands, BuildMarkers markers, BuildPaths paths, BuildEnvironment environment)
    : BuildCommandStep(markers, "configure")
{
    public override string Name => "Configure the build";

    protected override Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        commands.RunAsync(
            commands.CMake(
                "-S", CMakePaths.Of(paths.Source), "-B", CMakePaths.Of(paths.Build), "-G", "Ninja",
                "-DCMAKE_BUILD_TYPE=Release", "-DBUILD_TESTING=OFF",
                $"-DCMAKE_MAKE_PROGRAM={CMakePaths.Of(environment.Ninja)}",
                $"-DCMAKE_C_COMPILER={CMakePaths.Of(environment.Gcc)}",
                $"-DCMAKE_CXX_COMPILER={CMakePaths.Of(environment.Gxx)}"),
            failure => failure.SawCertificateError
                ? $"CMake could not download a dependency over HTTPS. CA bundle used: {environment.CaBundle}."
                : "CMake could not configure the build.",
            progress,
            cancellationToken);
}

/// <summary>Builds the default target. Ninja resumes from what an interrupted run already compiled.</summary>
public sealed class CompileStep(BuildCommands commands, BuildMarkers markers, BuildPaths paths, int jobs) : BuildCommandStep(markers, "compile")
{
    public override string Name => "Compile AnyPS5";

    protected override Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        commands.RunAsync(
            commands.CMake("--build", CMakePaths.Of(paths.Build), "--parallel", jobs.ToString(CultureInfo.InvariantCulture)),
            _ => "The build failed. The log holds the full compiler output.",
            progress,
            cancellationToken);
}

/// <summary>Builds the patched .prx libraries (target libs).</summary>
public sealed class LibsStep(BuildCommands commands, BuildMarkers markers, BuildPaths paths, int jobs) : BuildCommandStep(markers, "libs")
{
    public override string Name => "Build the libraries";

    protected override Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        commands.RunAsync(
            commands.CMake("--build", CMakePaths.Of(paths.Build), "--target", "libs", "--parallel", jobs.ToString(CultureInfo.InvariantCulture)),
            _ => "The build failed. The log holds the full compiler output.",
            progress,
            cancellationToken);
}
