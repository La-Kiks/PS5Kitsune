using System.ComponentModel;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Updates;

namespace PS5Kitsune.App.Tests;

public sealed class UpdateNoticeViewModelTests
{
    private readonly List<string> _opened = [];

    private static UpdateChecker Checker(StubHttpHandler handler) => new(new HttpClient(handler), NullLogger<UpdateChecker>.Instance);

    [Fact]
    public void Without_an_update_the_notice_is_hidden_and_the_button_disabled()
    {
        var notice = new UpdateNoticeViewModel(_opened.Add);

        Assert.False(notice.HasUpdate);
        Assert.Null(notice.Text);
        Assert.Null(notice.ButtonName);
        Assert.False(notice.OpenCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_newer_release_shows_the_notice_and_the_button_opens_its_page()
    {
        var notice = new UpdateNoticeViewModel(_opened.Add);

        await notice.CheckAsync(Checker(StubHttpHandler.Json("""{ "tag_name": "v0.2.0" }""")), new AppVersion(0, 1, 0, false));

        Assert.True(notice.HasUpdate);
        Assert.Equal("PS5Kitsune 0.2.0", notice.Text);
        Assert.Equal("Download PS5Kitsune 0.2.0", notice.ButtonName);
        Assert.True(notice.OpenCommand.CanExecute(null));

        notice.OpenCommand.Execute(null);

        Assert.Equal(["https://github.com/La-Kiks/PS5Kitsune/releases/tag/v0.2.0"], _opened);
    }

    [Fact]
    public async Task An_unknown_running_version_makes_no_request()
    {
        var handler = StubHttpHandler.Json("""{ "tag_name": "v0.2.0" }""");
        var notice = new UpdateNoticeViewModel(_opened.Add);

        await notice.CheckAsync(Checker(handler), null);

        Assert.Empty(handler.Requests);
        Assert.False(notice.HasUpdate);
    }

    [Fact]
    public async Task Up_to_date_keeps_the_notice_hidden()
    {
        var notice = new UpdateNoticeViewModel(_opened.Add);

        await notice.CheckAsync(Checker(StubHttpHandler.Json("""{ "tag_name": "v0.1.0" }""")), new AppVersion(0, 1, 0, false));

        Assert.False(notice.HasUpdate);
    }

    [Fact]
    public async Task A_failing_checker_keeps_the_notice_hidden()
    {
        var notice = new UpdateNoticeViewModel(_opened.Add);

        await notice.CheckAsync(Checker(StubHttpHandler.Json("{}", HttpStatusCode.InternalServerError)), new AppVersion(0, 1, 0, false));

        Assert.False(notice.HasUpdate);
    }

    [Fact]
    public async Task A_browser_that_cannot_be_opened_shows_a_message_with_the_address()
    {
        var notice = new UpdateNoticeViewModel(_ => throw new Win32Exception("No application is associated."));
        await notice.CheckAsync(Checker(StubHttpHandler.Json("""{ "tag_name": "v0.2.0" }""")), new AppVersion(0, 1, 0, false));

        notice.OpenCommand.Execute(null);

        Assert.Contains("No application is associated.", notice.OpenError, StringComparison.Ordinal);
        Assert.Contains("https://github.com/La-Kiks/PS5Kitsune/releases/tag/v0.2.0", notice.OpenError, StringComparison.Ordinal);
    }
}
