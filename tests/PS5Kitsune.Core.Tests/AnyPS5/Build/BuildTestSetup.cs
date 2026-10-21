using PS5Kitsune.Core.AnyPS5.Build;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.AnyPS5.Build;

/// <summary>Build services over a temp root, with MinGit and WinLibs files in place and a fake process runner.</summary>
internal sealed class BuildTestSetup
{
    public const string Commit = "48d61f53b46f1d0726889e4dd39c5ef2b3eb30cd";

    public static readonly Uri Repository = new("https://github.com/boykopovar/AnyPS5.git");

    public BuildTestSetup(string root, string commit = Commit)
    {
        Layout = new DataLayout(root);
        Profile = new AnyPS5Profile
        {
            SchemaVersion = AnyPS5Profile.CurrentSchemaVersion,
            Id = "test-profile",
            Source = new ProfileSource("release", Tag: "v0.1.2"),
            Build = new ProfileBuild(Repository, commit),
        };
        var unused = new ToolDefinition("1", new Uri("https://example.test/unused.zip"), new string('0', 64));
        MinGit = new MinGitTool(unused, Layout, Platform, new FakeDownloader(), Runner);
        WinLibs = new WinLibsTool(unused, Layout, Platform, new FakeDownloader(), Runner);
        var caBundle = Path.Combine(Layout.ToolDirectory(MinGitTool.ToolId), "ucrt64", "etc", "ssl", "certs", "ca-bundle.crt");
        foreach (var file in (string[])[MinGit.Executable, Path.Combine(MinGit.ShellDirectory, "bash" + Platform.ExecutableSuffix), caBundle, WinLibs.Gcc, WinLibs.Gxx, WinLibs.CMake, WinLibs.Ninja, .. WinLibs.RuntimeDlls])
        {
            TestFiles.Write(file, Path.GetFileName(file));
        }

        Environment = new BuildEnvironment(MinGit, WinLibs);
        Paths = new BuildPaths(Layout, Platform);
        Markers = new BuildMarkers(Paths, Profile);
        Commands = new BuildCommands(Runner, Environment, Paths, FreeSpace);
    }

    public DataLayout Layout { get; }

    public FakePlatform Platform { get; } = new();

    public FakeProcessRunner Runner { get; } = new();

    public FakeFreeSpace FreeSpace { get; } = new();

    public AnyPS5Profile Profile { get; }

    public MinGitTool MinGit { get; }

    public WinLibsTool WinLibs { get; }

    public BuildEnvironment Environment { get; }

    public BuildPaths Paths { get; }

    public BuildMarkers Markers { get; }

    public BuildCommands Commands { get; }
}
