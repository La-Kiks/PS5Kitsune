using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Tools;

public sealed class MesaToolTests : IDisposable
{
    private static readonly byte[] Archive = Convert.FromBase64String(
        "N3q8ryccAAPrlY7Y/wAAAAAAAAAcAAAAAAAAAOi3OSoBADZyZWFkbWV7IklDRCI6eyJsaWJyYXJ5X3BhdGgiOiIuXHZ1bGthbl9sdnAuZGxsIn19ZGxseDg2AOABkwC8XQAAgTMHrg/Rp0+UoJCgd166WD4RDBCY3aM1dc5wGlPAfkmjISyciJFkv+Y8uFGQRkcNAHQBkfjCW5uO9F1v2WbwOYqBEtJHwgAVto/rGoGh9iRdGWKfnBPmpgCHpayyLTjrkfFiextbGplDnkqEnq/+pWKt2GslPoDVpctWdSdv0VG4d3H7OyOipOBh/VWJgERsb5dVLwJ2fl4kTDYCKU08ee8n8a/r4R+giGd0/mqH2GjVBRNIG/++uAAAAAAXBjsBCYDEAAcLAQABISEBFgyBlAoBJT2LUQAA");

    private static readonly byte[] NoDriver = Convert.FromBase64String(
        "N3q8ryccAANqITCgqwAAAAAAAAAbAAAAAAAAAMc0v2EBACp7IklDRCI6eyJsaWJyYXJ5X3BhdGgiOiIuXHZ1bGthbl9sdnAuZGxsIn19AOAApgB0XQAAgTMHrg/Q8bH8nz9HQQQEMnr/wJP0rNXvnyHovKj7wNSTQRJIgdRPpO2g/CFJoEMDB+YcvhYdyjSrSa5YuLB6VGcKu7EFrU/+KkltCC8gukEbZS8aVsGo1JaNHTb1I/f2vxudRPryp89w9HSK7NkacQAAAAAXBi8BCXwABwsBAAEhIQEWDICnCgHClYxYAAA=");

    private static readonly ToolDefinition Definition = new("26.2.4", new Uri("https://example.test/mesa3d-26.2.4-release-msvc.7z"), new string('a', 64));

    private readonly TempDirectory _temp = new();
    private readonly FakeDownloader _downloader = new();

    private MesaTool CreateTool() => new(Definition, new DataLayout(_temp.Path), _downloader);

    [Fact]
    public async Task Install_keeps_only_lavapipe_flat_in_tools_mesa()
    {
        _downloader.Serve(Definition.Url, Archive);
        var tool = CreateTool();
        Assert.False(tool.IsInstalled());

        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(tool.IsInstalled());
        Assert.Equal(_temp.Combine("tools", "mesa", "lvp_icd.x86_64.json"), tool.Manifest);
        Assert.Equal("dll", await File.ReadAllTextAsync(_temp.Combine("tools", "mesa", "vulkan_lvp.dll"), TestContext.Current.CancellationToken));
        Assert.Equal(
            [".ps5kitsune-tool.json", "lvp_icd.x86_64.json", "vulkan_lvp.dll"],
            Directory.GetFileSystemEntries(_temp.Combine("tools", "mesa")).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal));
        Assert.Equal("26.2.4", await tool.GetVersionAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_archive_without_the_driver_is_refused_and_nothing_is_installed()
    {
        _downloader.Serve(Definition.Url, NoDriver);
        var tool = CreateTool();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("x64/vulkan_lvp.dll", exception.Message, StringComparison.Ordinal);
        Assert.False(tool.IsInstalled());
        Assert.False(Directory.Exists(_temp.Combine("tools", "mesa")));
    }

    [Fact]
    public async Task A_file_that_is_not_a_7z_is_refused()
    {
        _downloader.Serve(Definition.Url, "not an archive"u8.ToArray());
        var tool = CreateTool();

        await Assert.ThrowsAsync<InvalidDataException>(() => tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(tool.IsInstalled());
    }

    public void Dispose() => _temp.Dispose();
}
