using System.Runtime.Versioning;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Tests.Checks;

namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class LinuxChecksTests : IDisposable
{
    private readonly PS5Kitsune.Core.Tests.TempDirectory _temp = new();

    private static readonly DistroPackages Ubuntu = new(OsRelease.Parse("ID=ubuntu\n"));

    private string Device()
    {
        var path = _temp.Combine("udmabuf");
        File.WriteAllText(path, "");
        return path;
    }

    [Fact]
    public void Udmabuf_with_access_is_ok()
    {
        var result = new UdmabufCheck { DevicePath = Device(), CanReadWrite = _ => true }.Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Ok, result.Status);
        Assert.Equal("Read-write access.", result.Summary);
    }

    [Fact]
    public void Udmabuf_without_access_warns_with_the_kvm_group_fix()
    {
        var result = new UdmabufCheck { DevicePath = Device(), CanReadWrite = _ => false }.Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Equal("No read-write access to /dev/udmabuf; games copy shared memory instead of importing it, and some may fail.", result.Summary);
        Assert.Equal("sudo usermod -aG kvm $USER, then log out and in again.", result.Action);
    }

    [Fact]
    public void A_kernel_without_udmabuf_warns_without_a_fix()
    {
        var result = new UdmabufCheck { DevicePath = _temp.Combine("absent") }.Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Equal("This kernel does not provide /dev/udmabuf; games copy shared memory instead of importing it, and some may fail.", result.Summary);
        Assert.Null(result.Action);
    }

    [Fact]
    public void Udmabuf_is_not_needed_when_every_gpu_uses_nvidia_proprietary()
    {
        var facts = TestFacts.Healthy() with { Vulkan = TestFacts.Probe(TestFacts.GeForce) };

        var result = new UdmabufCheck { DevicePath = _temp.Combine("absent") }.Evaluate(facts);

        Assert.Equal(CheckStatus.Info, result.Status);
        Assert.Equal("Not needed: every GPU uses NVIDIA's proprietary driver.", result.Summary);
    }

    [Fact]
    public void Udmabuf_is_checked_when_another_gpu_needs_it()
    {
        var facts = TestFacts.Healthy() with { Vulkan = TestFacts.Probe(TestFacts.GeForce, TestFacts.Radeon) };

        Assert.Equal(CheckStatus.Warning, new UdmabufCheck { DevicePath = _temp.Combine("absent") }.Evaluate(facts).Status);
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void Python_found_is_ok_with_its_path()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var python = _temp.Combine("python3");
        File.WriteAllText(python, "");
        File.SetUnixFileMode(python, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var result = new PythonCheck(new SystemPython(Ubuntu) { SearchPath = _temp.Path }, Ubuntu).Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Ok, result.Status);
        Assert.Equal($"{python}.", result.Summary);
    }

    [Fact]
    public void Python_missing_blocks_with_the_install_command()
    {
        var result = new PythonCheck(new SystemPython(Ubuntu) { SearchPath = "" }, Ubuntu).Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Blocking, result.Status);
        Assert.Equal("python3 was not found.", result.Summary);
        Assert.Equal("sudo apt install python3", result.Action);
    }

    public void Dispose() => _temp.Dispose();
}
