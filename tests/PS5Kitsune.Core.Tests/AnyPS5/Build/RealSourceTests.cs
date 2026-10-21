using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.AnyPS5.Build;

/// <summary>
/// Downloads MinGit and fetches the real AnyPS5 commit and submodules (a few hundred MB).
/// Explicit: run it with -- --filter-class "PS5Kitsune.Core.Tests.AnyPS5.Build.RealSourceTests" --explicit on.
/// </summary>
public sealed class RealSourceTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact(Explicit = true)]
    [Trait("Category", "Network")]
    public async Task The_profile_commit_and_its_submodules_can_be_fetched()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("MinGit is Windows only.");
        }

        var services = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<IPlatform>(new FakePlatform())
            .AddPS5KitsuneCore(new DataLayout(_temp.Path), ProfileLoader.LoadEmbedded())
            .BuildServiceProvider();
        var steps = services.GetServices<IAnyPS5Source>().Single(source => source.Mode == AnyPS5Mode.Build).CreateSteps();
        var token = TestContext.Current.CancellationToken;

        // Install MinGit, get the source, update the submodules: WinLibs is not needed for git.
        foreach (var step in new[] { steps[0], steps[2], steps[3] })
        {
            await step.ExecuteAsync(null, token);
            Assert.True(step.IsAlreadyDone(), step.Name);
        }

        var lines = new List<string>();
        var commands = services.GetRequiredService<PS5Kitsune.Core.AnyPS5.Build.BuildCommands>();
        await services.GetRequiredService<IProcessRunner>().RunAsync(commands.Git("submodule", "status", "--recursive"), line => lines.Add(line.Text), token);
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.False(line.StartsWith('-') || line.StartsWith('+'), line));
    }

    public void Dispose() => _temp.Dispose();
}
