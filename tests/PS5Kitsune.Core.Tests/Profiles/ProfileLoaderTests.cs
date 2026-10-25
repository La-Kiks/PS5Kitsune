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
        Assert.True(profile.Validated);
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

    [Fact]
    public void Embedded_profile_has_check_thresholds()
    {
        var checks = ProfileLoader.LoadEmbedded().Checks;

        Assert.Equal(13824, checks.CommitLimitMiB);
        Assert.Equal(5, checks.MinFreeDiskGiB);
        Assert.Contains("610.47", checks.NvidiaBadDrivers);
    }

    [Fact]
    public void A_profile_without_checks_gets_the_default_thresholds()
    {
        var profile = Load("""
            { "schemaVersion": 1, "id": "minimal", "source": { "kind": "release", "tag": "v1" } }
            """);

        Assert.Equal(13824, profile.Checks.CommitLimitMiB);
        Assert.Equal(5, profile.Checks.MinFreeDiskGiB);
        Assert.Empty(profile.Checks.NvidiaBadDrivers);
    }

    [Fact]
    public void Embedded_profile_has_the_release_date()
    {
        var profile = ProfileLoader.LoadEmbedded();

        Assert.Equal(new DateTimeOffset(2026, 10, 14, 8, 56, 52, TimeSpan.Zero), profile.Source.PublishedAt);
    }

    [Fact]
    public void The_release_date_is_optional()
    {
        var profile = Load("""
            { "schemaVersion": 1, "id": "no-date", "source": { "kind": "release", "tag": "v1" } }
            """);

        Assert.Null(profile.Source.PublishedAt);
    }

    [Fact]
    public void Rejects_a_non_positive_commit_limit()
    {
        var exception = Assert.Throws<InvalidProfileException>(() => Load("""
            { "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" }, "checks": { "commitLimitMiB": 0 } }
            """));

        Assert.Contains("commitLimitMiB", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Embedded_profile_pins_the_build_commit_and_the_build_tools()
    {
        var profile = ProfileLoader.LoadEmbedded();

        Assert.Equal(new Uri("https://github.com/boykopovar/AnyPS5.git"), profile.Build?.Repository);
        Assert.Equal("3fd1cc393b8512c2e989860053f30dee5a8cd1aa", profile.Build?.Commit);
        Assert.Equal("da35e72aa21c005a5a0d298cfbae110bc1609a815730ea0dde84b01a1b3cd3be", profile.Tools["mingit"].Sha256);
        Assert.Equal("cb2fbad6162540cdf5e1facdce08d4dac359e8cf64f7f696a99274291763b815", profile.Tools["winlibs"].Sha256);
        Assert.Equal(10, profile.Checks.BuildMinFreeDiskGiB);
    }

    [Theory]
    [InlineData("http://github.com/boykopovar/AnyPS5.git", "48d61f53b46f1d0726889e4dd39c5ef2b3eb30cd")]
    [InlineData("https://github.com/boykopovar/AnyPS5.git", "48d61f5")]
    [InlineData("https://github.com/boykopovar/AnyPS5.git", "48D61F53B46F1D0726889E4DD39C5EF2B3EB30CD")]
    public void Rejects_an_invalid_build_section(string repository, string commit)
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "build": { "repository": "{{repository}}", "commit": "{{commit}}" }
            }
            """));
    }

    [Fact]
    public void Rejects_a_negative_build_disk_threshold()
    {
        Assert.Throws<InvalidProfileException>(() => Load("""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "checks": { "buildMinFreeDiskGiB": -1 }
            }
            """));
    }

    [Fact]
    public void RequireBuild_names_the_profile_when_it_has_no_build_section()
    {
        var profile = Load("""{ "schemaVersion": 1, "id": "release-only", "source": { "kind": "release", "tag": "v1" } }""");

        var exception = Assert.Throws<InvalidProfileException>(() => profile.RequireBuild());

        Assert.Contains("release-only", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Embedded_profile_gives_the_build_tool_sizes()
    {
        var profile = ProfileLoader.LoadEmbedded();

        Assert.Equal(12601679, profile.Tools["python"].Size);
        Assert.Equal(39806486, profile.Tools["mingit"].Size);
        Assert.Equal(267593703, profile.Tools["winlibs"].Size);
    }

    [Fact]
    public void A_tool_size_is_optional()
    {
        var profile = Load($$"""
            {
              "schemaVersion": 1, "id": "ok", "source": { "kind": "release", "tag": "v1" },
              "tools": { "python": { "version": "3", "url": "https://example.test/p.zip", "sha256": "{{new string('a', 64)}}" } }
            }
            """);

        Assert.Null(profile.Tools["python"].Size);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Rejects_a_tool_size_that_is_not_positive(long size)
    {
        var exception = Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "tools": { "python": { "version": "3", "url": "https://example.test/p.zip", "sha256": "{{new string('a', 64)}}", "size": {{size}} } }
            }
            """));

        Assert.Contains("size", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Embedded_profile_pins_mesa_and_the_fonts()
    {
        var profile = ProfileLoader.LoadEmbedded();

        var mesa = profile.Tools["mesa"];
        Assert.Equal("26.2.4", mesa.Version);
        Assert.Equal("351fc8c8b695878ffb3eaa044b3ead08672a48b1a045e3c3e3975811df0f6695", mesa.Sha256);
        Assert.Equal(24, profile.Fonts.Count);
        Assert.Equal(86_522_532, profile.Fonts.Sum(font => font.Size));
        Assert.Contains(profile.Fonts, font => font.Name == "NotoSansCJK-Regular.ttc");
    }

    [Theory]
    [InlineData("fonts/NotoSans-Regular.ttf")]
    [InlineData(@"fonts\NotoSans-Regular.ttf")]
    [InlineData("..")]
    [InlineData("")]
    [InlineData("C:x.ttf")]
    [InlineData("a.ttf:stream")]
    public void Rejects_a_font_name_that_is_not_a_plain_file_name(string name)
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "fonts": [ { "name": "{{name.Replace(@"\", @"\\", StringComparison.Ordinal)}}", "url": "https://example.test/f.ttf", "sha256": "{{new string('a', 64)}}", "size": 1 } ]
            }
            """));
    }

    [Fact]
    public void Rejects_a_font_listed_twice()
    {
        var font = $$"""{ "name": "NotoSans-Regular.ttf", "url": "https://example.test/f.ttf", "sha256": "{{new string('a', 64)}}", "size": 1 }""";
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "fonts": [ {{font}}, {{font}} ]
            }
            """));
    }

    [Fact]
    public void Rejects_a_font_over_plain_http()
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "fonts": [ { "name": "f.ttf", "url": "http://example.test/f.ttf", "sha256": "{{new string('a', 64)}}", "size": 1 } ]
            }
            """));
    }

    [Fact]
    public void The_embedded_profile_has_the_diagnostics_rules_in_order()
    {
        var diagnostics = ProfileLoader.LoadEmbedded().Diagnostics;

        Assert.Equal(new Uri("https://github.com/boykopovar/AnyPS5/discussions/categories/game-bring-up"), diagnostics.ReportUrl);
        Assert.Equal(
            ["nvidia-shader-compiler", "libs-not-dll", "libs-missing-dll", "commit-limit", "gpu-features", "cpu-too-old", "param-json",
             "invalid-setting", "invalid-setting", "invalid-setting", "invalid-setting", "not-implemented"],
            diagnostics.Rules.Select(rule => rule.Id));
        Assert.Equal("ANYPS5_PRESENT_MODE", diagnostics.Rules[10].Captures["variable"]);
    }

    [Fact]
    public void A_profile_without_diagnostics_has_no_rules()
    {
        var profile = Load("""{ "schemaVersion": 1, "id": "plain", "source": { "kind": "release", "tag": "v1" } }""");

        Assert.Empty(profile.Diagnostics.Rules);
        Assert.Null(profile.Diagnostics.ReportUrl);
    }

    [Theory]
    [InlineData("""{ "id": "gpu-features" }""")]
    [InlineData("""{ "id": "", "log": [ "x" ] }""")]
    [InlineData("""{ "id": "gpu-features", "log": [ "(unclosed" ] }""")]
    [InlineData("""{ "id": "cpu-too-old", "exitCodes": [ "3221225501" ] }""")]
    [InlineData("""{ "id": "gpu-features", "log": [ "x" ], "match": "most" }""")]
    public void Rejects_an_invalid_diagnostics_rule(string rule)
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "diagnostics": { "rules": [ {{rule}} ] }
            }
            """));
    }

    [Fact]
    public void Rejects_a_report_url_over_plain_http()
    {
        Assert.Throws<InvalidProfileException>(() => Load("""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "diagnostics": { "reportUrl": "http://example.test/report", "rules": [] }
            }
            """));
    }

    private static AnyPS5Profile Load(string json) => ProfileLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));
}
