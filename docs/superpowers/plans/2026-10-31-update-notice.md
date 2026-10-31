# Update Notice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The app checks the latest GitHub Release of PS5Kitsune at startup and, when it is newer, shows a notice in the sidebar whose button opens that release's page.

**Architecture:** Core gets an `Updates/` folder: `AppVersion` (parse and compare versions) and `UpdateChecker` (one GET to the GitHub API, never throws). `AppSettings` gains `CheckForUpdates`. The App gets an `UpdateNoticeViewModel`, exposed by `MainWindowViewModel.UpdateNotice` and shown in the sidebar; `App.axaml.cs` starts the check when the window opens.

**Tech Stack:** .NET 10, Avalonia 12.1.4, CommunityToolkit.Mvvm 8.4.2 (partial properties), System.Text.Json, Microsoft.Extensions.Logging (`[LoggerMessage]`), xUnit v3 4.0.2, Avalonia.Headless.

**Spec:** [docs/superpowers/specs/2026-10-31-update-notice-design.md](../specs/2026-10-31-update-notice-design.md)

## Global Constraints

- Release endpoint: `GET https://api.github.com/repos/La-Kiks/PS5Kitsune/releases/latest`, header `Accept: application/vnd.github+json`, through the shared `HttpClient` (its `User-Agent: PS5Kitsune/<version>` is already set in `AddPS5KitsuneCore`).
- Timeout: 10 seconds.
- Page address is built, never read from the response: `https://github.com/La-Kiks/PS5Kitsune/releases/tag/v{X.Y.Z}`.
- Only a tag of the form `vX.Y.Z` (no `-pre-release`) counts; strictly higher than the running version.
- A running build whose version has a `-` part (`0.0.0-ci.44`, `0.0.0-rehearsal`) sends no request.
- `settings.json` field: `checkForUpdates`, default `true`; schema version stays 1.
- Failures never throw and never show anything: one log line each, Information for no network or timeout, Warning for the rest.
- Sidebar texts: "Update available", "PS5Kitsune X.Y.Z", button "Download", accessible name "Download PS5Kitsune X.Y.Z".
- The CLI never calls `UpdateChecker`.
- Build rules: `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`. Core code uses `ConfigureAwait(false)`; App code uses `ConfigureAwait(true)`. Format strings with `CultureInfo.InvariantCulture`.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Commit locally only; no push.

## File Structure

| File | Responsibility |
|---|---|
| Create `src/PS5Kitsune.Core/Updates/AppVersion.cs` | Parse `X.Y.Z[-pre][+build]` with an optional `v`; compare; the running app's version |
| Create `src/PS5Kitsune.Core/Updates/UpdateChecker.cs` | `UpdateInfo` record and the GitHub check |
| Modify `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs` | Register `UpdateChecker` |
| Modify `src/PS5Kitsune.Core/Settings/AppSettings.cs` | `CheckForUpdates = true` |
| Create `src/PS5Kitsune.App/ViewModels/UpdateNoticeViewModel.cs` | The notice's state and its Download command |
| Modify `src/PS5Kitsune.App/ViewModels/MainWindowViewModel.cs` | Expose `UpdateNotice` |
| Modify `src/PS5Kitsune.App/Views/MainWindow.axaml` | The notice block in the sidebar |
| Modify `src/PS5Kitsune.App/App.axaml.cs` | Start the check when the window opens |
| Create `tests/PS5Kitsune.Core.Tests/Updates/AppVersionTests.cs` | Parsing and ordering |
| Create `tests/PS5Kitsune.Core.Tests/Updates/UpdateCheckerTests.cs` | Every answer GitHub can give |
| Create `tests/PS5Kitsune.Core.Tests/Fakes/StubHttpHandler.cs` | A canned `HttpMessageHandler` that records requests (linked into App.Tests) |
| Create `tests/PS5Kitsune.Core.Tests/Fakes/CapturingLogger.cs` | An `ILogger<T>` that records level and message |
| Modify `tests/PS5Kitsune.Core.Tests/Settings/AppSettingsStoreTests.cs` | The new field |
| Create `tests/PS5Kitsune.App.Tests/UpdateNoticeViewModelTests.cs` | View model behaviour |
| Modify `tests/PS5Kitsune.App.Tests/ShellTests.cs` | The block shows and hides in the real window |
| Modify `tests/PS5Kitsune.App.Tests/Screenshots.cs` | One extra render with the notice |
| Modify `tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj` | Link `StubHttpHandler.cs` |
| Modify `README.md` | Where `settings.json` is and how to turn the check off |

---

### Task 1: `AppVersion`

**Files:**
- Create: `src/PS5Kitsune.Core/Updates/AppVersion.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Updates/AppVersionTests.cs`

**Interfaces:**
- Produces: `public readonly record struct AppVersion(int Major, int Minor, int Patch, bool IsPreRelease) : IComparable<AppVersion>` with `static AppVersion? Parse(string text)`, `static AppVersion? Current { get; }`, `int CompareTo(AppVersion other)` (major, minor, patch only), operators `<`, `>`, `<=`, `>=`, and `ToString()` → `"X.Y.Z"`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Updates/AppVersionTests.cs`:

```csharp
using PS5Kitsune.Core.Updates;

namespace PS5Kitsune.Core.Tests.Updates;

public sealed class AppVersionTests
{
    [Theory]
    [InlineData("0.1.0", 0, 1, 0, false)]
    [InlineData("v0.2.0", 0, 2, 0, false)]
    [InlineData("0.1.0+1a00b0e32ea0d0d6d220a81908e313fc9894cf7e", 0, 1, 0, false)]
    [InlineData("0.0.0-ci.44", 0, 0, 0, true)]
    [InlineData("1.2.3-rc.1+abc", 1, 2, 3, true)]
    [InlineData("10.20.30", 10, 20, 30, false)]
    public void Parses_a_version(string text, int major, int minor, int patch, bool preRelease)
    {
        Assert.Equal(new AppVersion(major, minor, patch, preRelease), AppVersion.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("0.1")]
    [InlineData("0.1.0.0")]
    [InlineData("a.b.c")]
    [InlineData("0.1.0-")]
    [InlineData(" 0.1.0")]
    [InlineData("0.1.0 ")]
    [InlineData("-1.0.0")]
    [InlineData("+abc")]
    public void Rejects_what_is_not_a_version(string text)
    {
        Assert.Null(AppVersion.Parse(text));
    }

    [Fact]
    public void Orders_by_major_minor_and_patch_as_numbers()
    {
        string[] ordered = ["0.1.0", "0.1.1", "0.2.0", "0.10.0", "1.0.0"];
        var versions = ordered.Select(text => AppVersion.Parse(text)!.Value).ToList();

        for (var i = 1; i < versions.Count; i++)
        {
            Assert.True(versions[i - 1] < versions[i], $"{ordered[i - 1]} < {ordered[i]}");
            Assert.True(versions[i] > versions[i - 1], $"{ordered[i]} > {ordered[i - 1]}");
        }
    }

    [Fact]
    public void The_build_part_does_not_count()
    {
        Assert.Equal(AppVersion.Parse("0.1.0"), AppVersion.Parse("0.1.0+other"));
        Assert.Equal(0, AppVersion.Parse("0.1.0")!.Value.CompareTo(AppVersion.Parse("0.1.0+other")!.Value));
    }

    [Fact]
    public void Prints_major_minor_and_patch()
    {
        Assert.Equal("1.2.3", AppVersion.Parse("v1.2.3-rc.1+abc")!.Value.ToString());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build FAILS with `CS0246: The type or namespace name 'AppVersion' could not be found` (the test project does not compile yet).

- [ ] **Step 3: Write the implementation**

`src/PS5Kitsune.Core/Updates/AppVersion.cs`:

```csharp
using System.Globalization;
using System.Reflection;

namespace PS5Kitsune.Core.Updates;

/// <summary>
/// A PS5Kitsune version: major.minor.patch, and whether it is a pre-release (a <c>-</c> part, as CI builds have).
/// The build part after <c>+</c> (the commit) is ignored. Comparison uses major, minor and patch only.
/// </summary>
public readonly record struct AppVersion(int Major, int Minor, int Patch, bool IsPreRelease) : IComparable<AppVersion>
{
    /// <summary>The running app's version, from the entry assembly's informational version; null when it has none that parses.</summary>
    public static AppVersion? Current =>
        Parse(Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "");

    /// <summary>Reads <c>X.Y.Z</c> with an optional leading <c>v</c>, <c>-pre-release</c> and <c>+build</c>; null for anything else.</summary>
    public static AppVersion? Parse(string text)
    {
        var core = text.StartsWith('v') ? text[1..] : text;
        var plus = core.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            core = core[..plus];
        }

        var preRelease = false;
        var dash = core.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            if (dash == 0 || dash == core.Length - 1)
            {
                return null;
            }

            preRelease = true;
            core = core[..dash];
        }

        var parts = core.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var numbers = new int[3];
        for (var i = 0; i < 3; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return null;
            }
        }

        return new AppVersion(numbers[0], numbers[1], numbers[2], preRelease);
    }

    public int CompareTo(AppVersion other) => (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));

    public static bool operator <(AppVersion left, AppVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(AppVersion left, AppVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(AppVersion left, AppVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(AppVersion left, AppVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --project tests/PS5Kitsune.Core.Tests -c Release --no-build -- --filter-class "PS5Kitsune.Core.Tests.Updates.AppVersionTests"`
Expected: all `AppVersionTests` PASS, build has 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Updates/AppVersion.cs tests/PS5Kitsune.Core.Tests/Updates/AppVersionTests.cs
git commit -m "Add AppVersion: parse and compare PS5Kitsune versions" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `UpdateChecker`

**Files:**
- Create: `src/PS5Kitsune.Core/Updates/UpdateChecker.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Fakes/StubHttpHandler.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Fakes/CapturingLogger.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs` (after `services.AddSingleton<IDownloader, HttpDownloader>();`)
- Test: `tests/PS5Kitsune.Core.Tests/Updates/UpdateCheckerTests.cs`

**Interfaces:**
- Consumes: `AppVersion` (Task 1).
- Produces:
  - `public sealed record UpdateInfo(AppVersion Version, Uri ReleasePage);`
  - `public sealed partial class UpdateChecker` with `public const string Repository = "La-Kiks/PS5Kitsune";`, `public static Uri LatestReleaseUrl { get; }`, `public UpdateChecker(HttpClient httpClient, ILogger<UpdateChecker> logger)`, `internal UpdateChecker(HttpClient httpClient, ILogger<UpdateChecker> logger, TimeSpan timeout)`, `public Task<UpdateInfo?> CheckAsync(AppVersion current, CancellationToken cancellationToken = default)`.
  - Test helpers: `internal sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler` with `List<HttpRequestMessage> Requests` and `static StubHttpHandler Json(string body, HttpStatusCode status = HttpStatusCode.OK)`; `internal sealed class CapturingLogger<T> : ILogger<T>` with `List<(LogLevel Level, string Message)> Entries`. Both in namespace `PS5Kitsune.Core.Tests.Fakes`.

- [ ] **Step 1: Write the test helpers**

`tests/PS5Kitsune.Core.Tests/Fakes/StubHttpHandler.cs`:

```csharp
using System.Net;
using System.Text;

namespace PS5Kitsune.Core.Tests.Fakes;

/// <summary>Answers every request with <paramref name="respond"/> and records the requests.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHttpHandler Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return respond(request, cancellationToken);
    }
}
```

`tests/PS5Kitsune.Core.Tests/Fakes/CapturingLogger.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace PS5Kitsune.Core.Tests.Fakes;

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
```

- [ ] **Step 2: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Updates/UpdateCheckerTests.cs`:

```csharp
using System.Net;
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
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build FAILS with `CS0246: The type or namespace name 'UpdateChecker' could not be found`.

- [ ] **Step 4: Write the implementation**

`src/PS5Kitsune.Core/Updates/UpdateChecker.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PS5Kitsune.Core.Updates;

/// <param name="Version">The newer release's version.</param>
/// <param name="ReleasePage">Its page on GitHub, built from the version.</param>
public sealed record UpdateInfo(AppVersion Version, Uri ReleasePage);

/// <summary>
/// Asks GitHub for PS5Kitsune's latest release. Only the app calls it, once per start; see the update notice spec.
/// A failure is logged and gives null: it never throws, except when the caller cancels.
/// </summary>
public sealed partial class UpdateChecker
{
    /// <summary>The app only ever looks for its own releases.</summary>
    public const string Repository = "La-Kiks/PS5Kitsune";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateChecker> _logger;
    private readonly TimeSpan _timeout;

    public UpdateChecker(HttpClient httpClient, ILogger<UpdateChecker> logger)
        : this(httpClient, logger, DefaultTimeout)
    {
    }

    internal UpdateChecker(HttpClient httpClient, ILogger<UpdateChecker> logger, TimeSpan timeout)
    {
        _httpClient = httpClient;
        _logger = logger;
        _timeout = timeout;
    }

    /// <summary>The latest published release; GitHub leaves out drafts and pre-releases.</summary>
    public static Uri LatestReleaseUrl { get; } = new("https://api.github.com/repos/" + Repository + "/releases/latest");

    /// <returns>The latest release when it is newer than <paramref name="current"/>; null otherwise, and for a pre-release build without asking.</returns>
    public async Task<UpdateInfo?> CheckAsync(AppVersion current, CancellationToken cancellationToken = default)
    {
        if (current.IsPreRelease)
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        string body;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await _httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogErrorAnswer((int)response.StatusCode);
                return null;
            }

            body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimedOut(_timeout.TotalSeconds);
            return null;
        }
        catch (HttpRequestException exception)
        {
            LogUnreachable(exception.Message);
            return null;
        }

        var tag = ReadTag(body);
        if (tag is null)
        {
            LogNoTag();
            return null;
        }

        if (!tag.StartsWith('v') || AppVersion.Parse(tag) is not { IsPreRelease: false } latest)
        {
            LogNotAVersion(tag);
            return null;
        }

        return latest > current ? new UpdateInfo(latest, new Uri("https://github.com/" + Repository + "/releases/tag/v" + latest)) : null;
    }

    /// <returns>The <c>tag_name</c> string; null when the body is not JSON, not an object, or has no string <c>tag_name</c>.</returns>
    private static string? ReadTag(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("tag_name", out var tag) && tag.ValueKind == JsonValueKind.String
                ? tag.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Update check: GitHub is unreachable ({Reason}).")]
    private partial void LogUnreachable(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update check: GitHub did not answer within {Seconds} seconds.")]
    private partial void LogTimedOut(double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check: GitHub answered {Status}.")]
    private partial void LogErrorAnswer(int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check: the answer has no release tag.")]
    private partial void LogNoTag();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check: the latest release's tag {Tag} is not a vX.Y.Z version.")]
    private partial void LogNotAVersion(string tag);
}
```

In `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`, add `using PS5Kitsune.Core.Updates;` with the other usings, and after `services.AddSingleton<IDownloader, HttpDownloader>();` add:

```csharp
        services.AddSingleton<UpdateChecker>();
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --project tests/PS5Kitsune.Core.Tests -c Release --no-build -- --filter-class "PS5Kitsune.Core.Tests.Updates.UpdateCheckerTests"`
Expected: all `UpdateCheckerTests` PASS, build has 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add src/PS5Kitsune.Core/Updates/UpdateChecker.cs src/PS5Kitsune.Core/ServiceCollectionExtensions.cs tests/PS5Kitsune.Core.Tests/Fakes/StubHttpHandler.cs tests/PS5Kitsune.Core.Tests/Fakes/CapturingLogger.cs tests/PS5Kitsune.Core.Tests/Updates/UpdateCheckerTests.cs
git commit -m "Add UpdateChecker: ask GitHub for the latest release, never throw" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `checkForUpdates` in `settings.json`

**Files:**
- Modify: `src/PS5Kitsune.Core/Settings/AppSettings.cs`
- Modify: `README.md:48` (the sentence ending "is saved in `settings.json`.")
- Test: `tests/PS5Kitsune.Core.Tests/Settings/AppSettingsStoreTests.cs`

**Interfaces:**
- Produces: `public sealed record AppSettings(int SchemaVersion, string? DataRoot, AppTheme Theme = AppTheme.System, bool CheckForUpdates = true)`; JSON name `checkForUpdates`.

- [ ] **Step 1: Write the failing tests**

Append inside `AppSettingsStoreTests` in `tests/PS5Kitsune.Core.Tests/Settings/AppSettingsStoreTests.cs`:

```csharp
    [Fact]
    public async Task A_file_without_checkForUpdates_checks_for_updates()
    {
        Directory.CreateDirectory(_temp.Combine("settings"));
        await File.WriteAllTextAsync(_temp.Combine("settings", "settings.json"), """{ "schemaVersion": 1, "dataRoot": null, "theme": "dark" }""", TestContext.Current.CancellationToken);

        var load = Store.Load();

        Assert.Null(load.Problem);
        Assert.True(load.Settings.CheckForUpdates);
    }

    [Fact]
    public async Task Turning_the_check_off_is_saved_and_kept_when_the_theme_changes()
    {
        await Store.SaveAsync(AppSettings.Default with { CheckForUpdates = false }, TestContext.Current.CancellationToken);

        Assert.Contains("\"checkForUpdates\": false", await File.ReadAllTextAsync(Store.FilePath, TestContext.Current.CancellationToken), StringComparison.Ordinal);

        await Store.UpdateAsync(settings => settings with { Theme = AppTheme.Dark }, TestContext.Current.CancellationToken);

        var settings = Store.Load().Settings;
        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.False(settings.CheckForUpdates);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build FAILS with `CS0117: 'AppSettings' does not contain a definition for 'CheckForUpdates'`.

- [ ] **Step 3: Write the implementation**

Replace the record in `src/PS5Kitsune.Core/Settings/AppSettings.cs`:

```csharp
/// <summary>Contents of settings.json, shared by the app and the CLI.</summary>
/// <param name="SchemaVersion">Version of the file format.</param>
/// <param name="DataRoot">The data root chosen in the app; null for the platform's default.</param>
/// <param name="Theme">The app's theme; absent in older files, which means <see cref="AppTheme.System"/>.</param>
/// <param name="CheckForUpdates">Whether the app asks GitHub for a newer release at startup; absent in older files, which means true.</param>
public sealed record AppSettings(int SchemaVersion, string? DataRoot, AppTheme Theme = AppTheme.System, bool CheckForUpdates = true)
```

(The body with `CurrentSchemaVersion = 1` and `Default` is unchanged.)

In `README.md`, replace "The sidebar's theme switch (System, Light, Dark) is saved in `settings.json`." with:

```markdown
The sidebar's theme switch (System, Light, Dark) is saved in `settings.json` (`%LOCALAPPDATA%\PS5Kitsune\settings.json` on Windows, `~/.config/ps5kitsune/settings.json` on Linux). At each start, the app asks GitHub whether a newer PS5Kitsune is out and, if so, shows it at the bottom of the sidebar with a Download button that opens the release page; to turn this off, set `"checkForUpdates": false` in `settings.json`. The command-line tool never checks.
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --project tests/PS5Kitsune.Core.Tests -c Release --no-build -- --filter-class "PS5Kitsune.Core.Tests.Settings.AppSettingsStoreTests"`
Expected: all `AppSettingsStoreTests` PASS (the existing ones too).

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Settings/AppSettings.cs tests/PS5Kitsune.Core.Tests/Settings/AppSettingsStoreTests.cs README.md
git commit -m "Add checkForUpdates to settings.json, on by default" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The sidebar notice

**Files:**
- Create: `src/PS5Kitsune.App/ViewModels/UpdateNoticeViewModel.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/MainWindowViewModel.cs` (constructor and a new property)
- Modify: `src/PS5Kitsune.App/Views/MainWindow.axaml` (the sidebar's `Grid.Row="2"` block)
- Modify: `src/PS5Kitsune.App/App.axaml.cs`
- Modify: `tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj` (link `StubHttpHandler.cs`)
- Test: `tests/PS5Kitsune.App.Tests/UpdateNoticeViewModelTests.cs`, `tests/PS5Kitsune.App.Tests/ShellTests.cs`, `tests/PS5Kitsune.App.Tests/Screenshots.cs`

**Interfaces:**
- Consumes: `UpdateChecker`, `UpdateInfo`, `AppVersion` (Tasks 1-2), `AppSettings.CheckForUpdates` (Task 3), `IPlatform.OpenPath(string path)` (exists).
- Produces: `public sealed partial class UpdateNoticeViewModel(Action<string> open) : ObservableObject` with `UpdateInfo? Update { get; set; }` (observable), `bool HasUpdate`, `string? Text`, `string? ButtonName`, `IRelayCommand OpenCommand`, `Task CheckAsync(UpdateChecker checker, AppVersion? current)`; `MainWindowViewModel.UpdateNotice { get; }` and a new last optional constructor parameter `UpdateNoticeViewModel? updateNotice = null`.

The spec put the notice's properties on `MainWindowViewModel` itself; they live in their own small view model instead, exposed as `MainWindowViewModel.UpdateNotice`, so the 7 existing `new MainWindowViewModel(...)` calls in tests stay unchanged and the notice is tested on its own. Behaviour and texts are as specified.

- [ ] **Step 1: Link the stub handler into the App tests**

In `tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj`, add to the `<ItemGroup>` holding the other `Compile Include` links:

```xml
    <Compile Include="..\PS5Kitsune.Core.Tests\Fakes\StubHttpHandler.cs" Link="StubHttpHandler.cs" />
```

- [ ] **Step 2: Write the failing view model tests**

`tests/PS5Kitsune.App.Tests/UpdateNoticeViewModelTests.cs`:

```csharp
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
}
```

Append to `ShellTests` in `tests/PS5Kitsune.App.Tests/ShellTests.cs` (add `using PS5Kitsune.Core.Updates;` at the top):

```csharp
    [Fact]
    public Task The_update_notice_shows_in_the_sidebar_only_with_an_update() => session.RunAsync(() =>
    {
        var notice = new UpdateNoticeViewModel(_ => { });
        var viewModel = new MainWindowViewModel(new Navigator(), new FakeDialogs(), new FakePages(), new ThemeService(new AppSettingsStore(_temp.Combine("s")), _ => { }), updateNotice: notice);
        var window = new MainWindow { DataContext = viewModel };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button? Download() => window.GetVisualDescendants().OfType<Button>().SingleOrDefault(button => Equals(button.Content, "Download"));

            Assert.False(Download()?.IsEffectivelyVisible ?? false);

            notice.Update = new UpdateInfo(new AppVersion(0, 2, 0, false), new Uri("https://github.com/La-Kiks/PS5Kitsune/releases/tag/v0.2.0"));
            Dispatcher.UIThread.RunJobs();

            var download = Download();
            Assert.NotNull(download);
            Assert.True(download.IsEffectivelyVisible);
            Assert.Equal("Download PS5Kitsune 0.2.0", Avalonia.Automation.AutomationProperties.GetName(download));
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "PS5Kitsune 0.2.0" && text.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    });
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build FAILS with `CS0246: The type or namespace name 'UpdateNoticeViewModel' could not be found`.

- [ ] **Step 4: Write the view model**

`src/PS5Kitsune.App/ViewModels/UpdateNoticeViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.Core.Updates;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The sidebar's update notice: hidden until the check finds a newer release, then a button that opens its page.</summary>
/// <param name="open">Opens a web page; the app passes <c>IPlatform.OpenPath</c>.</param>
public sealed partial class UpdateNoticeViewModel(Action<string> open) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate), nameof(Text), nameof(ButtonName))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    public partial UpdateInfo? Update { get; set; }

    public bool HasUpdate => Update is not null;

    public string? Text => Update is { } update ? "PS5Kitsune " + update.Version : null;

    public string? ButtonName => Update is { } update ? "Download PS5Kitsune " + update.Version : null;

    /// <summary>Checks once; does nothing when the running version is unknown. Never throws: the checker logs its failures.</summary>
    public async Task CheckAsync(UpdateChecker checker, AppVersion? current)
    {
        if (current is not { } version)
        {
            return;
        }

        Update = await checker.CheckAsync(version).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasUpdate))]
    private void Open()
    {
        if (Update is { } update)
        {
            open(update.ReleasePage.ToString());
        }
    }
}
```

- [ ] **Step 5: Expose it from `MainWindowViewModel`**

In `src/PS5Kitsune.App/ViewModels/MainWindowViewModel.cs`, change the constructor signature and add the assignment and property:

```csharp
    public MainWindowViewModel(Navigator navigator, IDialogService dialogs, IPages pages, ThemeService theme, Func<ILogger>? loggerSource = null, UpdateNoticeViewModel? updateNotice = null)
    {
        Navigator = navigator;
        _dialogs = dialogs;
        _pages = pages;
        _theme = theme;
        _loggerSource = loggerSource;
        UpdateNotice = updateNotice ?? new UpdateNoticeViewModel(_ => { });
```

(the rest of the constructor is unchanged), and below `public Navigator Navigator { get; }`:

```csharp
    /// <summary>The sidebar's update notice; usable while the page runs work, since it only opens the browser.</summary>
    public UpdateNoticeViewModel UpdateNotice { get; }
```

- [ ] **Step 6: Add the block to the sidebar**

In `src/PS5Kitsune.App/Views/MainWindow.axaml`, replace the sidebar's bottom row — the `<StackPanel Grid.Row="2" Orientation="Horizontal" ...>` holding the three theme `RadioButton`s — with a vertical `StackPanel` that holds the notice, then that same theme switch unchanged:

```xml
        <StackPanel Grid.Row="2" Spacing="16">
          <StackPanel Spacing="4" Margin="4,0" IsVisible="{Binding UpdateNotice.HasUpdate}">
            <TextBlock Text="Update available" Classes="caption" />
            <TextBlock Text="{Binding UpdateNotice.Text}" TextWrapping="Wrap" />
            <Button Classes="accent" Content="Download" Command="{Binding UpdateNotice.OpenCommand}" HorizontalAlignment="Left"
                    AutomationProperties.Name="{Binding UpdateNotice.ButtonName}" ToolTip.Tip="Opens the release page in your browser" />
          </StackPanel>

          <StackPanel Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center"
                      c:ArrowSelection.IsEnabled="True">
            <RadioButton GroupName="Theme" Theme="{StaticResource {x:Type ToggleButton}}" Content="◐" Padding="10,4"
                         IsChecked="{Binding IsSystemTheme}" ToolTip.Tip="System theme" AutomationProperties.Name="System theme" />
            <RadioButton GroupName="Theme" Theme="{StaticResource {x:Type ToggleButton}}" Content="☀" Padding="10,4"
                         IsChecked="{Binding IsLightTheme}" ToolTip.Tip="Light theme" AutomationProperties.Name="Light theme" />
            <RadioButton GroupName="Theme" Theme="{StaticResource {x:Type ToggleButton}}" Content="☾" Padding="10,4"
                         IsChecked="{Binding IsDarkTheme}" ToolTip.Tip="Dark theme" AutomationProperties.Name="Dark theme" />
          </StackPanel>
        </StackPanel>
```

- [ ] **Step 7: Start the check from `App.axaml.cs`**

In `src/PS5Kitsune.App/App.axaml.cs`, add `using PS5Kitsune.Core.Updates;`, then replace the line `window.DataContext = new MainWindowViewModel(navigator, dialogs, pages, theme, () => sessions.Current.Get<ILogger<MainWindowViewModel>>());` with:

```csharp
            var updateNotice = new UpdateNoticeViewModel(platform.OpenPath);
            window.DataContext = new MainWindowViewModel(navigator, dialogs, pages, theme, () => sessions.Current.Get<ILogger<MainWindowViewModel>>(), updateNotice);
            if (loaded.Settings.CheckForUpdates)
            {
                // In the background once the window is up: startup never waits for GitHub, and a failure only logs a line.
                window.Opened += async (_, _) => await updateNotice.CheckAsync(sessions.Current.Get<UpdateChecker>(), AppVersion.Current).ConfigureAwait(true);
            }
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release --no-build`
Expected: every test PASSES (the new `UpdateNoticeViewModelTests` and `ShellTests.The_update_notice_shows_in_the_sidebar_only_with_an_update`, and the existing `TabOrderTests`, `FocusTests`, `FocusSettleTests`, which must not see the hidden button), build has 0 warnings. If a tab-order test now lists the Download button, the block is not hidden: check the `IsVisible` binding.

- [ ] **Step 9: Render the notice for the PR**

In `tests/PS5Kitsune.App.Tests/Screenshots.cs`, inside `Render_every_page`, after the `foreach (var (name, page) in await gallery.BuildAsync())` loop and still inside the `try`, add a render of the Home page with the notice set:

```csharp
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                await session.RunAsync(() =>
                {
                    Application.Current!.RequestedThemeVariant = variant;
                    var notice = new UpdateNoticeViewModel(_ => { })
                    {
                        Update = new PS5Kitsune.Core.Updates.UpdateInfo(new PS5Kitsune.Core.Updates.AppVersion(0, 2, 0, false), new Uri("https://github.com/La-Kiks/PS5Kitsune/releases/tag/v0.2.0")),
                    };
                    var window = new MainWindow
                    {
                        DataContext = new MainWindowViewModel(new Navigator(), new FakeDialogs(), new FakePages(), new ThemeService(new AppSettingsStore(gallery.SettingsFolder), _ => { }), updateNotice: notice),
                        Width = 860,
                        Height = 680,
                    };
                    try
                    {
                        window.Show();
                        Dispatcher.UIThread.RunJobs();
                        window.CaptureRenderedFrame()!.Save(Path.Combine(folder, $"update-notice-{variant.Key}.png"), new PngBitmapEncoderOptions());
                    }
                    finally
                    {
                        window.Close();
                    }
                });
            }
```

Run (Git Bash): `PS5K_SCREENSHOTS="$(pwd)/TestResults/screenshots" dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "PS5Kitsune.App.Tests.Screenshots"`
Expected: PASS, and `TestResults/screenshots/update-notice-Light.png` and `update-notice-Dark.png` exist. Open both: the sidebar shows "Update available", "PS5Kitsune 0.2.0" and an accent "Download" button above the theme switch, nothing cut off at 168 px, readable in both themes. `TestResults/` is git-ignored: the images are attached to the PR, not committed.

- [ ] **Step 10: Commit**

```bash
git add src/PS5Kitsune.App/ViewModels/UpdateNoticeViewModel.cs src/PS5Kitsune.App/ViewModels/MainWindowViewModel.cs src/PS5Kitsune.App/Views/MainWindow.axaml src/PS5Kitsune.App/App.axaml.cs tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj tests/PS5Kitsune.App.Tests/UpdateNoticeViewModelTests.cs tests/PS5Kitsune.App.Tests/ShellTests.cs tests/PS5Kitsune.App.Tests/Screenshots.cs
git commit -m "Show the update notice in the sidebar and check at startup" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Manual criterion

No code. Run on Windows, from the repository root, with the network on. `v0.1.0` is the latest release.

- [ ] **Step 1: An older build sees the latest release**

Run: `dotnet run --project src/PS5Kitsune.App -c Release -p:Version=0.0.1`
Expected: within a few seconds of the window opening, the sidebar shows "Update available", "PS5Kitsune 0.1.0" and Download. Clicking Download opens `https://github.com/La-Kiks/PS5Kitsune/releases/tag/v0.1.0` in the browser. Close the app.

- [ ] **Step 2: The current build sees nothing**

Run: `dotnet run --project src/PS5Kitsune.App -c Release -p:Version=0.1.0`
Expected: no notice. Close the app.

- [ ] **Step 3: Turned off, nothing is asked**

Set `"checkForUpdates": false` in `%LOCALAPPDATA%\PS5Kitsune\settings.json`, then run Step 1's command again.
Expected: no notice, and the app log (the newest `app-*.log` in the data root's `logs` folder, `C:\PS5Kitsune\logs` by default) has no "Update check" line. Set the value back to `true`.

- [ ] **Step 4: Offline, nothing shows and one line is logged**

Turn the network off (Wi-Fi off), run Step 1's command.
Expected: no notice; the newest app log has one line "Update check: GitHub is unreachable (…)" at Info. Turn the network back on.

- [ ] **Step 5: Record the results**

Write the four results in the PR body's "Validation and proof" with the version used. If a step fails, stop and report: do not change the plan's code to make it pass without a review.
