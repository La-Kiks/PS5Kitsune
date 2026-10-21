using PS5Kitsune.Core.AnyPS5.Build;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.AnyPS5.Build;

public sealed class BuildStepsTests : IDisposable
{
    private const string Commit = BuildTestSetup.Commit;

    private readonly TempDirectory _temp = new();
    private readonly BuildTestSetup _setup;

    public BuildStepsTests()
    {
        _setup = new BuildTestSetup(_temp.Path);
    }

    private GetSourceStep Source => new(_setup.Commands, _setup.Markers, _setup.Paths, _setup.Profile.Build!);

    private static string Slashes(string path) => path.Replace('\\', '/');

    private List<string[]> Arguments => [.. _setup.Runner.Calls.Select(call => call.Arguments.ToArray())];

    [Fact]
    public async Task Getting_the_source_into_an_empty_folder_fetches_the_commit_only()
    {
        await Source.ExecuteAsync(null, TestContext.Current.CancellationToken);

        string[][] expected =
        [
            ["init"],
            ["config", "core.longpaths", "true"],
            ["config", "remote.origin.url", "https://github.com/boykopovar/AnyPS5.git"],
            ["fetch", "--depth", "1", "origin", Commit],
            ["checkout", "--detach", "--force", Commit],
        ];
        Assert.Equal(expected, Arguments);
        Assert.All(_setup.Runner.Calls, call => Assert.Equal(_setup.Paths.Source, call.WorkingDirectory));
        Assert.True(Source.IsAlreadyDone());
    }

    [Fact]
    public async Task A_new_commit_makes_the_source_stale()
    {
        await Source.ExecuteAsync(null, TestContext.Current.CancellationToken);

        var newer = new BuildTestSetup(_temp.Path, new string('b', 40));

        Assert.False(new GetSourceStep(newer.Commands, newer.Markers, newer.Paths, newer.Profile.Build!).IsAlreadyDone());
    }

    [Fact]
    public async Task A_failed_fetch_says_to_check_the_connection()
    {
        _setup.Runner.Respond = spec => spec.Arguments[0] == "fetch" ? new FakeProcessResult(128, StandardError: ["fatal: unable to access"]) : new FakeProcessResult(0);

        var exception = await Assert.ThrowsAsync<BuildCommandException>(() => Source.ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.StartsWith("Cannot download from GitHub: check the connection, then run install again.", exception.Message, StringComparison.Ordinal);
        Assert.False(Source.IsAlreadyDone());
    }

    [Fact]
    public async Task Submodules_are_synced_then_updated_at_depth_1()
    {
        await new SubmodulesStep(_setup.Commands, _setup.Markers).ExecuteAsync(null, TestContext.Current.CancellationToken);

        string[][] expected =
        [
            ["submodule", "sync", "--recursive"],
            ["submodule", "update", "--init", "--recursive", "--depth", "1", "--force"],
        ];
        Assert.Equal(expected, Arguments);
    }

    [Fact]
    public async Task Configure_uses_Ninja_and_the_WinLibs_compilers()
    {
        await new ConfigureStep(_setup.Commands, _setup.Markers, _setup.Paths, _setup.Environment).ExecuteAsync(null, TestContext.Current.CancellationToken);

        var call = Assert.Single(_setup.Runner.Calls);
        Assert.Equal(_setup.WinLibs.CMake, call.FileName);
        Assert.Equal(
            [
                "-S", Slashes(_setup.Paths.Source), "-B", Slashes(_setup.Paths.Build), "-G", "Ninja",
                "-DCMAKE_BUILD_TYPE=Release", "-DBUILD_TESTING=OFF",
                $"-DCMAKE_MAKE_PROGRAM={Slashes(_setup.WinLibs.Ninja)}",
                $"-DCMAKE_C_COMPILER={Slashes(_setup.WinLibs.Gcc)}",
                $"-DCMAKE_CXX_COMPILER={Slashes(_setup.WinLibs.Gxx)}",
            ],
            call.Arguments);
    }

    [Fact]
    public async Task A_configure_certificate_error_names_the_CA_bundle()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(1, StandardError: ["  status_code: 60"]);

        var exception = await Assert.ThrowsAsync<BuildCommandException>(() =>
            new ConfigureStep(_setup.Commands, _setup.Markers, _setup.Paths, _setup.Environment).ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.StartsWith($"CMake could not download a dependency over HTTPS. CA bundle used: {_setup.Environment.CaBundle}.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Another_configure_failure_has_its_own_message()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(1, StandardError: ["CMake Error at CMakeLists.txt:1"]);

        var exception = await Assert.ThrowsAsync<BuildCommandException>(() =>
            new ConfigureStep(_setup.Commands, _setup.Markers, _setup.Paths, _setup.Environment).ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.StartsWith("CMake could not configure the build.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compile_and_libs_build_with_the_given_jobs()
    {
        await new CompileStep(_setup.Commands, _setup.Markers, _setup.Paths, 6).ExecuteAsync(null, TestContext.Current.CancellationToken);
        await new LibsStep(_setup.Commands, _setup.Markers, _setup.Paths, 6).ExecuteAsync(null, TestContext.Current.CancellationToken);

        string[][] expected =
        [
            ["--build", Slashes(_setup.Paths.Build), "--parallel", "6"],
            ["--build", Slashes(_setup.Paths.Build), "--target", "libs", "--parallel", "6"],
        ];
        Assert.Equal(expected, Arguments);
        Assert.True(_setup.Markers.IsCurrent("compile"));
        Assert.True(_setup.Markers.IsCurrent("libs"));
    }

    [Fact]
    public async Task A_failed_compile_points_to_the_log()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(1, ["FAILED: a.obj"]);

        var exception = await Assert.ThrowsAsync<BuildCommandException>(() =>
            new CompileStep(_setup.Commands, _setup.Markers, _setup.Paths, 2).ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.StartsWith("The build failed. The log holds the full compiler output.", exception.Message, StringComparison.Ordinal);
        Assert.False(_setup.Markers.IsCurrent("compile"));
    }

    public void Dispose() => _temp.Dispose();
}
