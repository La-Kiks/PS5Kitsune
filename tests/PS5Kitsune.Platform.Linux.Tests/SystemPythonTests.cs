using System.Runtime.Versioning;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class SystemPythonTests : IDisposable
{
    private readonly PS5Kitsune.Core.Tests.TempDirectory _temp = new();

    private static readonly DistroPackages Ubuntu = new(OsRelease.Parse("ID=ubuntu\n"));

    [Fact]
    [SupportedOSPlatform("linux")]
    public void Finds_an_executable_python3_on_the_path()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var bin = _temp.Combine("bin");
        Directory.CreateDirectory(bin);
        var python = Path.Combine(bin, "python3");
        File.WriteAllText(python, "#!/bin/sh\n");
        File.SetUnixFileMode(python, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var runtime = new SystemPython(Ubuntu) { SearchPath = $"relative:{_temp.Combine("empty")}:{bin}" };

        Assert.True(runtime.IsInstalled());
        Assert.Equal(python, runtime.Executable);
        Assert.Equal(python, runtime.CreateScriptSpec("/s/import_audit.py", ["--json", "a.json"]).FileName);
        Assert.Equal("Find Python 3", runtime.StepName);
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void A_python3_without_its_execute_bit_does_not_count()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var python = _temp.Combine("python3");
        File.WriteAllText(python, "");
        File.SetUnixFileMode(python, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        Assert.False(new SystemPython(Ubuntu) { SearchPath = _temp.Path }.IsInstalled());
    }

    [Fact]
    public async Task Without_python3_install_names_the_command()
    {
        var runtime = new SystemPython(Ubuntu) { SearchPath = "" };

        var exception = await Assert.ThrowsAsync<PythonMissingException>(() => runtime.InstallAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Python 3 is required: sudo apt install python3", exception.Message);
    }

    public void Dispose() => _temp.Dispose();
}
