using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.AnyPS5.Build;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.AnyPS5;

/// <summary>A profile whose assets are served by a <see cref="FakeDownloader"/>, and an installer wired to it.</summary>
internal sealed class AnyPS5TestSetup
{
    public const string Commit = "48d61f53b46f1d0726889e4dd39c5ef2b3eb30cd";

    public static readonly byte[] MinGitArchive = TestFiles.ZipBytes(
        ("cmd/git.exe", "git"u8.ToArray()),
        ("usr/bin/sh.exe", "bash"u8.ToArray()),
        ("ucrt64/etc/ssl/certs/ca-bundle.crt", "certs"u8.ToArray()));

    public static readonly byte[] WinLibsArchive = TestFiles.ZipBytes(
        ("mingw64/bin/gcc.exe", "gcc"u8.ToArray()),
        ("mingw64/bin/g++.exe", "g++"u8.ToArray()),
        ("mingw64/bin/cmake.exe", "cmake"u8.ToArray()),
        ("mingw64/bin/ninja.exe", "ninja"u8.ToArray()),
        ("mingw64/bin/libgcc_s_seh-1.dll", "dll"u8.ToArray()),
        ("mingw64/bin/libstdc++-6.dll", "dll"u8.ToArray()),
        ("mingw64/bin/libwinpthread-1.dll", "dll"u8.ToArray()));

    public static readonly byte[] RelinkerContent = "fake relinker"u8.ToArray();

    public static readonly byte[] LibsArchive = TestFiles.ZipBytes(
        ("libs/libSceAudioOut.prx", "prx"u8.ToArray()),
        ("libs/anyps5.dll", "dll"u8.ToArray()),
        ("README.md", "not extracted"u8.ToArray()));

    public static readonly byte[] SourceArchive = TestFiles.ZipBytes(
        ("AnyPS5-0.1.2/tools/import_audit.py", "audit"u8.ToArray()),
        ("AnyPS5-0.1.2/tools/nid_names.py", "names"u8.ToArray()),
        ("AnyPS5-0.1.2/core/libs/prx/libc/stub.cpp", "cpp"u8.ToArray()),
        ("AnyPS5-0.1.2/docs/BUILD.md", "not extracted"u8.ToArray()));

    public static readonly byte[] UnwrapScriptContent = "unwrap"u8.ToArray();

    public static readonly byte[] PythonArchive = TestFiles.ZipBytes(("python.exe", "fake"u8.ToArray()), ("python314._pth", "python314.zip"u8.ToArray()));

    public static readonly Uri SourceUrl = new("https://example.test/AnyPS5-v0.1.2.zip");

    public static readonly Uri UnwrapScriptUrl = new("https://example.test/360e8bd/tools/unwrap_self.py");

    private static readonly Uri MinGitUrl = new("https://example.test/mingit.zip");

    private static readonly Uri WinLibsUrl = new("https://example.test/winlibs.zip");

    private static readonly Uri PythonUrl = new("https://example.test/python-embed.zip");

    public AnyPS5TestSetup(string root, string commit = Commit)
    {
        Layout = new DataLayout(root);
        Profile = new AnyPS5Profile
        {
            SchemaVersion = AnyPS5Profile.CurrentSchemaVersion,
            Id = "test-profile",
            Source = new ProfileSource("release", Tag: "v0.1.2"),
            Build = new ProfileBuild(new Uri("https://github.com/boykopovar/AnyPS5.git"), commit),
            Assets = new Dictionary<string, ProfileAsset>
            {
                [ProfileAssetKeys.Relinker] = new("relinker-v0.1.2.exe", TestFiles.Sha256(RelinkerContent)),
                [ProfileAssetKeys.Libs] = new("prx-windows-v0.1.2.zip", TestFiles.Sha256(LibsArchive)),
                [ProfileAssetKeys.Scripts] = new(null, TestFiles.Sha256(SourceArchive), SourceUrl, ["tools/", "core/libs/prx/"]),
                [ProfileAssetKeys.UnwrapScript] = new(null, TestFiles.Sha256(UnwrapScriptContent), UnwrapScriptUrl),
            },
            Tools = new Dictionary<string, ToolDefinition>
            {
                [PythonEmbeddableTool.ToolId] = new("3.14.8", PythonUrl, TestFiles.Sha256(PythonArchive)),
                [MinGitTool.ToolId] = new("2.56.0.2", MinGitUrl, TestFiles.Sha256(MinGitArchive)),
                [WinLibsTool.ToolId] = new("15.2.0-r7", WinLibsUrl, TestFiles.Sha256(WinLibsArchive)),
            },
            Os = ProfileOs.Windows,
            Relinker = new RelinkerOptions { Args = ["--windows", "--registry"], IntelArg = "--to-intel" },
        };

        Downloader.Serve(Profile.AssetUrl(ProfileAssetKeys.Relinker), RelinkerContent);
        Downloader.Serve(Profile.AssetUrl(ProfileAssetKeys.Libs), LibsArchive);
        Downloader.Serve(SourceUrl, SourceArchive);
        Downloader.Serve(UnwrapScriptUrl, UnwrapScriptContent);
        Downloader.Serve(PythonUrl, PythonArchive);

        Downloader.Serve(MinGitUrl, MinGitArchive);
        Downloader.Serve(WinLibsUrl, WinLibsArchive);

        PythonTool = new PythonEmbeddableTool(Profile.Tools[PythonEmbeddableTool.ToolId], Layout, Platform, Downloader, Runner);
        var mingit = new MinGitTool(Profile.Tools[MinGitTool.ToolId], Layout, Platform, Downloader, Runner);
        var winlibs = new WinLibsTool(Profile.Tools[WinLibsTool.ToolId], Layout, Platform, Downloader, Runner);
        var environment = new BuildEnvironment(mingit, winlibs);
        Paths = new AnyPS5Paths(Layout, Platform);
        BuildPaths = new BuildPaths(Layout, Platform);
        var markers = new BuildMarkers(BuildPaths, Profile);
        var commands = new BuildCommands(Runner, environment, BuildPaths, new FakeFreeSpace());
        ReleaseSource = new ReleaseSource(Profile, Paths, Layout, Downloader);
        BuildSource = new BuildSource(Profile, mingit, winlibs, environment, BuildPaths, markers, commands, Paths);
        Installer = new AnyPS5Installer([ReleaseSource, BuildSource], PythonTool, Paths, StepRunner, Time);
    }

    public DataLayout Layout { get; }

    public FakePlatform Platform { get; } = new();

    public FakeDownloader Downloader { get; } = new();

    public FakeProcessRunner Runner { get; } = new();

    public FixedTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));

    public AnyPS5Profile Profile { get; }

    public PythonEmbeddableTool PythonTool { get; }

    public AnyPS5Paths Paths { get; }

    public StepRunner StepRunner { get; } = new(NullLogger<StepRunner>.Instance);

    public BuildPaths BuildPaths { get; }

    public ReleaseSource ReleaseSource { get; }

    public BuildSource BuildSource { get; }

    public AnyPS5Installer Installer { get; }

    public Task InstallAsync(AnyPS5Mode mode = AnyPS5Mode.Release) => Installer.InstallAsync(mode, cancellationToken: TestContext.Current.CancellationToken);

    /// <summary>Makes the fake process runner produce what CMake would: the relinker after the build, the .prx after libs, and the scripts after checkout.</summary>
    public void PlayTheBuild(string relinker = "built relinker")
    {
        Runner.Respond = spec =>
        {
            if (spec.Arguments is ["checkout", ..])
            {
                TestFiles.Write(Path.Combine(BuildPaths.Source, "tools", "import_audit.py"), "audit");
                TestFiles.Write(Path.Combine(BuildPaths.Source, "tools", "unwrap_self.py"), "unwrap");
                TestFiles.Write(Path.Combine(BuildPaths.Source, "core", "libs", "prx", "libc", "stub.cpp"), "cpp");
            }
            else if (spec.Arguments is ["--build", _, "--parallel", _])
            {
                TestFiles.Write(BuildPaths.BuiltRelinker, relinker);
            }
            else if (spec.Arguments is ["--build", _, "--target", "libs", ..])
            {
                TestFiles.Write(Path.Combine(BuildPaths.BuiltLibs, "libSceAudioOut.prx"), "prx");
            }

            return new FakeProcessResult(0);
        };
    }
}
