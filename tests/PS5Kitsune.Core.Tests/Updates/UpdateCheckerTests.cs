using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Updates;

namespace PS5Kitsune.Core.Tests.Updates;

public sealed class UpdateCheckerTests
{
    private static readonly AppVersion Running = new(0, 1, 0, false);

    private readonly CapturingLogger<UpdateChecker> _logger = new();

    private UpdateChecker Checker(StubHttpHandler handler, TimeSpan? timeout = null) =>
        new(new HttpClient(handler), _logger, timeout ?? TimeSpan.FromSeconds(10));

    private static string Release(string tag, string htmlUrl = "https://github.com/La-Kiks/PS5Kitsune/releases/tag/x") =>
        $$"""{ "tag_name": "{{tag}}", "html_url": "{{htmlUrl}}", "draft": false, "prerelease": false }""";

    [Fact]
    public async Task A_newer_release_gives_its_version_and_page()
    {
        var update = await Checker(StubHttpHandler.Json(Release("v0.2.0"))).CheckAsync(Running, TestContext.Current.CancellationToken);

        Assert.Equal(new UpdateInfo(new AppVersion(0, 2, 0, false), new Uri("https://github.com/La-Kiks/PS5Kitsune/releases/tag/v0.2.0")), update);
        Assert.Empty(_logger.Entries);
    }

    [Theory]
    [InlineData("v0.1.0")]
    [InlineData("v0.0.9")]
    public async Task The_same_or_an_older_release_gives_nothing(string tag)
    {
        Assert.Null(await Checker(StubHttpHandler.Json(Release(tag))).CheckAsync(Running, TestContext.Current.CancellationToken));
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task Asks_the_latest_release_of_this_repository_as_GitHub_JSON()
    {
        var handler = StubHttpHandler.Json(Release("v0.2.0"));

        await Checker(handler).CheckAsync(Running, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("https://api.github.com/repos/La-Kiks/PS5Kitsune/releases/latest"), request.RequestUri);
        Assert.Contains("application/vnd.github+json", request.Headers.Accept.Select(value => value.MediaType));
    }

    [Fact]
    public async Task The_page_is_built_from_the_tag_not_read_from_the_answer()
    {
        var update = await Checker(StubHttpHandler.Json(Release("v0.2.0", "https://evil.example/download"))).CheckAsync(Running, TestContext.Current.CancellationToken);

        Assert.Equal(new Uri("https://github.com/La-Kiks/PS5Kitsune/releases/tag/v0.2.0"), update!.ReleasePage);
    }

    [Theory]
    [InlineData("0.0.0-ci.44")]
    [InlineData("0.0.0-rehearsal")]
    public async Task A_pre_release_build_sends_no_request(string running)
    {
        var handler = StubHttpHandler.Json(Release("v0.2.0"));

        Assert.Null(await Checker(handler).CheckAsync(AppVersion.Parse(running)!.Value, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task An_error_answer_gives_nothing_and_one_warning(HttpStatusCode status)
    {
        Assert.Null(await Checker(StubHttpHandler.Json("{}", status)).CheckAsync(Running, TestContext.Current.CancellationToken));

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), entry.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "name": "no tag" }""")]
    [InlineData("""{ "tag_name": 12 }""")]
    [InlineData("""{ "tag_name": "nightly" }""")]
    [InlineData("""{ "tag_name": "0.2.0" }""")]
    [InlineData("""{ "tag_name": "v0.2.0-rc.1" }""")]
    public async Task An_answer_without_a_release_version_gives_nothing_and_one_warning(string body)
    {
        Assert.Null(await Checker(StubHttpHandler.Json(body)).CheckAsync(Running, TestContext.Current.CancellationToken));

        Assert.Equal(LogLevel.Warning, Assert.Single(_logger.Entries).Level);
    }

    [Fact]
    public async Task No_network_gives_nothing_and_one_information_line()
    {
        var handler = new StubHttpHandler((_, _) => throw new HttpRequestException("No such host is known."));

        Assert.Null(await Checker(handler).CheckAsync(Running, TestContext.Current.CancellationToken));

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("No such host is known.", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_slow_answer_times_out_gives_nothing_and_one_information_line()
    {
        var handler = new StubHttpHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        Assert.Null(await Checker(handler, TimeSpan.FromMilliseconds(50)).CheckAsync(Running, TestContext.Current.CancellationToken));

        Assert.Equal(LogLevel.Information, Assert.Single(_logger.Entries).Level);
    }

    [Fact]
    public async Task Cancelling_the_caller_throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var handler = new StubHttpHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Checker(handler).CheckAsync(Running, cancelled.Token));
    }

    [Fact]
    public async Task An_unknown_charset_in_the_answer_does_not_throw()
    {
        var handler = new StubHttpHandler((_, _) =>
        {
            var content = new StringContent("""{ "tag_name": "v0.2.0" }""");
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json; charset=nonsense-charset");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });

        var update = await Checker(handler).CheckAsync(Running, TestContext.Current.CancellationToken);

        Assert.Equal(new AppVersion(0, 2, 0, false), update!.Version);
    }
}
