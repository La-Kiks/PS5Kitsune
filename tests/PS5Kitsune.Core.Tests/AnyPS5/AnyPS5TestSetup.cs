using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.AnyPS5;

/// <summary>A profile whose assets are served by a <see cref="FakeDownloader"/>, and an installer wired to it.</summary>
internal sealed class AnyPS5TestSetup
{
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

    private static readonly Uri PythonUrl = new("https://example.test/python-embed.zip");

    public AnyPS5TestSetup(string root)
    {
        Layout = new DataLayout(root);
        Profile = new AnyPS5Profile
        {
            SchemaVersion = AnyPS5Profile.CurrentSchemaVersion,
            Id = "test-profile",
            Source = new ProfileSource("release", Tag: "v0.1.2"),
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
            },
            Relinker = new RelinkerOptions { WindowsArgs = ["--windows", "--registry"], IntelArg = "--to-intel" },
        };

        Downloader.Serve(Profile.AssetUrl(ProfileAssetKeys.Relinker), RelinkerContent);
        Downloader.Serve(Profile.AssetUrl(ProfileAssetKeys.Libs), LibsArchive);
        Downloader.Serve(SourceUrl, SourceArchive);
        Downloader.Serve(UnwrapScriptUrl, UnwrapScriptContent);
        Downloader.Serve(PythonUrl, PythonArchive);

        PythonTool = new PythonEmbeddableTool(Profile.Tools[PythonEmbeddableTool.ToolId], Layout, Platform, Downloader, Runner);
        Paths = new AnyPS5Paths(Layout, Platform);
        ReleaseSource = new ReleaseSource(Profile, Paths, Layout, Downloader);
        Installer = new AnyPS5Installer([ReleaseSource], PythonTool, Paths, StepRunner, Time);
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

    public ReleaseSource ReleaseSource { get; }

    public AnyPS5Installer Installer { get; }

    public Task InstallAsync(AnyPS5Mode mode = AnyPS5Mode.Release) => Installer.InstallAsync(mode, cancellationToken: TestContext.Current.CancellationToken);
}
