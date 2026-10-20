using System.Text;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tests.Profiles;

public sealed class ProfileLoaderTests
{
    [Fact]
    public void Embedded_profile_loads_with_a_pinned_python()
    {
        var profile = ProfileLoader.LoadEmbedded();

        Assert.Equal(AnyPS5Profile.CurrentSchemaVersion, profile.SchemaVersion);
        Assert.Equal("release", profile.Source.Kind);
        var python = profile.Tools["python"];
        Assert.Equal(Uri.UriSchemeHttps, python.Url.Scheme);
        Assert.Equal(64, python.Sha256.Length);
        Assert.Contains("--windows", profile.Relinker.WindowsArgs);
    }

    [Fact]
    public void Rejects_an_unknown_schema_version()
    {
        var exception = Assert.Throws<InvalidProfileException>(() => Load("""
            { "schemaVersion": 2, "id": "future", "source": { "kind": "release", "tag": "v9" } }
            """));

        Assert.Contains("schema version 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_tool_without_a_valid_hash()
    {
        Assert.Throws<InvalidProfileException>(() => Load("""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "tools": { "python": { "version": "3", "url": "https://example.test/p.zip", "sha256": "ABC" } }
            }
            """));
    }

    [Fact]
    public void Rejects_a_tool_downloaded_over_plain_http()
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "tools": { "python": { "version": "3", "url": "http://example.test/p.zip", "sha256": "{{new string('a', 64)}}" } }
            }
            """));
    }

    [Fact]
    public void Rejects_malformed_json()
    {
        Assert.Throws<InvalidProfileException>(() => Load("{ not json"));
    }

    [Fact]
    public void Embedded_profile_resolves_its_assets()
    {
        var profile = ProfileLoader.LoadEmbedded();

        Assert.Equal(
            new Uri("https://github.com/boykopovar/AnyPS5/releases/download/v0.1.2/relinker-v0.1.2.exe"),
            profile.AssetUrl(ProfileAssetKeys.Relinker));
        Assert.Equal(
            new Uri("https://github.com/boykopovar/AnyPS5/releases/download/v0.1.2/prx-windows-v0.1.2.zip"),
            profile.AssetUrl(ProfileAssetKeys.Libs));
        Assert.Equal(new Uri("https://github.com/boykopovar/AnyPS5/archive/refs/tags/v0.1.2.zip"), profile.AssetUrl(ProfileAssetKeys.Scripts));
        Assert.Equal(["tools/", "core/libs/prx/"], profile.Asset(ProfileAssetKeys.Scripts).Include!);
        Assert.Contains("360e8bd685d32b0e9d37e6ee2fae44417c0f808c", profile.AssetUrl(ProfileAssetKeys.UnwrapScript).AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("v0.1.2", profile.Source.Version);
    }

    [Fact]
    public void An_unknown_asset_is_an_invalid_profile()
    {
        Assert.Throws<InvalidProfileException>(() => ProfileLoader.LoadEmbedded().AssetUrl("missing"));
    }

    [Fact]
    public void Rejects_an_asset_with_neither_url_nor_name()
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "assets": { "scripts": { "sha256": "{{new string('a', 64)}}" } }
            }
            """));
    }

    [Fact]
    public void Rejects_an_asset_downloaded_over_plain_http()
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "assets": { "scripts": { "url": "http://example.test/s.zip", "sha256": "{{new string('a', 64)}}" } }
            }
            """));
    }

    private static AnyPS5Profile Load(string json) => ProfileLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));
}
