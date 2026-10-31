using System.Runtime.Versioning;
using System.Security.Cryptography;
using PS5Kitsune.Core.AnyPS5.Steps;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.AnyPS5.Steps;

public sealed class DownloadFileStepTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task A_program_that_lost_its_execute_bit_gets_it_back_without_a_new_download()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        byte[] content = [1, 2, 3, 4];
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        var destination = _temp.Combine("tools", "relinker");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllBytes(destination, content);
        File.SetUnixFileMode(destination, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var downloader = new FakeDownloader();
        var step = new DownloadFileStep("relinker", new Uri("https://example.invalid/relinker"), destination, sha256, downloader, executable: true);

        Assert.False(step.IsAlreadyDone());
        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Empty(downloader.Requests);
        Assert.True(ExecutableBits.Has(destination));
        Assert.True(step.IsAlreadyDone());
    }

    public void Dispose() => _temp.Dispose();
}
