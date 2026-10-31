using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tests.Profiles;

public sealed class ActiveProfileTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private DataLayout Layout => new(_temp.Path);

    private string OverridePath => _temp.Combine("profiles", "override.json");

    [Fact]
    public void Without_an_override_the_embedded_profile_is_active()
    {
        var active = ProfileLoader.LoadActive(Layout);

        Assert.Equal(ProfileLoader.LoadEmbedded().Id, active.Profile.Id);
        Assert.Equal(ProfileOrigin.Embedded, active.Origin);
        Assert.Null(active.Problem);
    }

    [Fact]
    public void A_valid_override_replaces_the_embedded_profile()
    {
        TestFiles.Write(OverridePath, """{ "schemaVersion": 2, "os": "windows", "id": "my-override", "source": { "kind": "release", "tag": "v9" } }""");

        var active = ProfileLoader.LoadActive(Layout);

        Assert.Equal("my-override", active.Profile.Id);
        Assert.Equal(ProfileOrigin.Override, active.Origin);
        Assert.Null(active.Problem);
    }

    [Fact]
    public void An_invalid_override_is_ignored_with_a_problem()
    {
        TestFiles.Write(OverridePath, "{ not json");

        var active = ProfileLoader.LoadActive(Layout);

        Assert.Equal(ProfileOrigin.Embedded, active.Origin);
        Assert.Equal(ProfileLoader.LoadEmbedded().Id, active.Profile.Id);
        Assert.Contains("override.json", active.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_override_is_ignored_with_a_problem()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to open a file another handle holds with FileShare.None.");
        }

        TestFiles.Write(OverridePath, """{ "schemaVersion": 2, "os": "windows", "id": "locked", "source": { "kind": "release", "tag": "v9" } }""");
        using var lockHandle = new FileStream(OverridePath, FileMode.Open, FileAccess.Read, FileShare.None);

        var active = ProfileLoader.LoadActive(Layout);

        Assert.Equal(ProfileOrigin.Embedded, active.Origin);
        Assert.Contains("override.json", active.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_os_gets_its_own_embedded_profile()
    {
        Assert.Equal(ProfileOs.Windows, ProfileLoader.LoadActive(Layout, ProfileOs.Windows).Profile.Os);
        Assert.Equal(ProfileOs.Linux, ProfileLoader.LoadActive(Layout, ProfileOs.Linux).Profile.Os);
    }

    [Fact]
    public void An_override_for_the_other_os_is_ignored_with_a_problem()
    {
        TestFiles.Write(OverridePath, """{ "schemaVersion": 2, "os": "windows", "id": "windows-override", "source": { "kind": "release", "tag": "v9" } }""");

        var active = ProfileLoader.LoadActive(Layout, ProfileOs.Linux);

        Assert.Equal(ProfileOrigin.Embedded, active.Origin);
        Assert.Equal(ProfileOs.Linux, active.Profile.Os);
        Assert.Contains("is for windows, not linux", active.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_schema_1_override_is_ignored_with_a_problem()
    {
        TestFiles.Write(OverridePath, """{ "schemaVersion": 1, "id": "old", "source": { "kind": "release", "tag": "v9" } }""");

        var active = ProfileLoader.LoadActive(Layout);

        Assert.Equal(ProfileOrigin.Embedded, active.Origin);
        Assert.Contains("schema version 1", active.Problem, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
