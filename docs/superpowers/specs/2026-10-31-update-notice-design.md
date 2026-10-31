# Update notice: design

October 31, 2026 · Ships in PS5Kitsune 0.2.0. Complements [docs/releasing.md](../../releasing.md).

## Goal

When a newer PS5Kitsune is published on GitHub, the app says so in its sidebar, and a button opens that release's page in the browser. The user downloads and replaces the file themselves.

It has to ship in 0.2.0 itself: a build can only announce versions newer than itself, so 0.1.0 users are never told about 0.2.0. Every later version is announced by the one before it.

**Criterion:** a build stamped `0.1.0`, started while `v0.2.0` is the latest release, shows the notice, and its button opens `https://github.com/La-Kiks/PS5Kitsune/releases/tag/v0.2.0`. With `"checkForUpdates": false`, or offline, nothing shows and the app starts as before.

**Out of scope:**

- **Downloading or installing the update.** No download, no SHA-256 check, no swap of the running file, no restart. These can come later on top of the same check.
- **The CLI.** `ps5kitsune` never contacts GitHub by itself: scripts should not make network calls they did not ask for. `--version` is unchanged.
- **A settings page or a toggle in the app.** The check is turned off in `settings.json` only.
- **Dismissing or skipping a version.** The notice is one small line; it stays until the user is on the new version.
- **Pre-release channels.** Only the latest published, non-pre-release GitHub Release counts.

## Behaviour

**When:** once per app start, in the background after the main window is shown. Startup never waits for it. The request gives up after 10 seconds.

**Which version is newer:** the latest release's tag (`v0.2.0`) without its `v`, compared with the running app's informational version (`0.1.0+1a00b0e…`) without its `+` build part. The comparison is on major, minor and patch as numbers. The notice shows only when the release is strictly higher.

**No request at all when:**

- `checkForUpdates` is `false` in `settings.json`;
- the running build is a pre-release: its version has a `-` part, such as `0.0.0-ci.44` (CI) or `0.0.0-rehearsal`. Such builds would otherwise always look out of date.

**Nothing shows, and the app carries on, when** the request fails (no network, DNS failure, timeout), GitHub answers with anything but 200 (404 when there is no release yet, 403 when the rate limit is reached, 5xx), the body is not JSON, has no `tag_name`, or the tag is not a version of the form `vX.Y.Z`. Each failure writes one line to the app log, at Information level for "no network or timeout" and Warning for the rest. No dialog, no error on screen.

**What the user sees:** at the bottom of the sidebar, above the theme switch:

```
  Update available
  PS5Kitsune 0.2.0
  [Download]
```

The block stacks three lines (a caption, the version, then the accent Download button) to fit the 168 px sidebar.

- [Download] opens the release page in the default browser with `IPlatform.OpenPath`, which already opens https URLs on Windows and Linux. When the browser cannot be opened (no handler for https, no `xdg-open`), a short message under the button says so and gives the address to open by hand; the app does not crash.
- The block is hidden until a newer version is found. It uses the existing sidebar styles in both themes. The button is reached with Tab like the other sidebar controls, and its accessible name is "Download PS5Kitsune 0.2.0".
- The button works while the page is busy (an install, an add): it only opens the browser.

**The page address is built, not read:** `https://github.com/La-Kiks/PS5Kitsune/releases/tag/v{version}` from the checked version. The `html_url` field of the response is not used, so an odd or tampered response cannot make the app open another website.

## Turning it off

`settings.json` gets one field, `checkForUpdates`, `true` by default:

```json
{
  "schemaVersion": 1,
  "dataRoot": null,
  "theme": "system",
  "checkForUpdates": false
}
```

A file without the field reads as `true`, so the schema version stays 1. Saving the theme or the data root keeps the field's value (both already save with `settings with { … }`). The README's settings paragraph says where the file is (`%LOCALAPPDATA%\PS5Kitsune\settings.json` on Windows, `~/.config/ps5kitsune/settings.json` on Linux) and how to turn the check off.

## Code

### Core: `src/PS5Kitsune.Core/Updates/`

- **`AppVersion`** (record struct): `Major`, `Minor`, `Patch`, `IsPreRelease`.
  - `static AppVersion? Parse(string text)`: accepts `X.Y.Z`, an optional leading `v`, an optional `-pre-release` part (sets `IsPreRelease`) and an optional `+build` part (ignored). Returns null for anything else: missing parts, non-numbers, extra parts, empty text.
  - Implements `IComparable<AppVersion>` on major, minor and patch.
  - `static AppVersion? Current`: the entry assembly's `AssemblyInformationalVersionAttribute`, parsed.
- **`UpdateInfo`** (record): `AppVersion Version`, `Uri ReleasePage`.
- **`UpdateChecker`**: `Task<UpdateInfo?> CheckAsync(AppVersion current, CancellationToken cancellationToken)`.
  - Returns null without a request when `current.IsPreRelease`.
  - Sends `GET https://api.github.com/repos/La-Kiks/PS5Kitsune/releases/latest` with `Accept: application/vnd.github+json`, through the shared `HttpClient` (its `User-Agent: PS5Kitsune/<version>` is required by the GitHub API), with a linked 10-second timeout.
  - Reads `tag_name` with `System.Text.Json`, parses it with `AppVersion.Parse`, and rejects a pre-release tag.
  - Returns `UpdateInfo` only when the tag's version is higher than `current`; null otherwise, logging the failures listed above.
  - Never throws, except `OperationCanceledException` when the caller's token is cancelled.
  - The repository (`La-Kiks/PS5Kitsune`) is a constant in the class: the app only ever looks for its own releases.
- Registered as a singleton in `AddPS5KitsuneCore`.

### Settings

`AppSettings` gains `bool CheckForUpdates = true`, after `Theme`.

### App

- **`UpdateNoticeViewModel(Action<string> open)`**:
  - `UpdateInfo? Update` (observable), `bool HasUpdate`, `string? Text` ("PS5Kitsune 0.2.0") and `string? ButtonName` ("Download PS5Kitsune 0.2.0");
  - `OpenCommand`: calls `open(Update.ReleasePage.ToString())` through `PathOpener.TryRun`, enabled only when `HasUpdate`; a failure sets `string? OpenError` (the reason and the address), shown under the button;
  - `Task CheckAsync(UpdateChecker checker, AppVersion? current)`: does nothing when `current` is null; otherwise awaits the check and sets `Update`.
- **`MainWindowViewModel`** exposes it as `UpdateNotice`, an optional last constructor parameter.
- **`App.axaml.cs`**: creates the view model with `platform.OpenPath`. When `loaded.Settings.CheckForUpdates` is true, the window's `Opened` event runs `CheckAsync` without awaiting it, inside a `try`/`catch` that logs one Warning line, so a bug in the check cannot end the app. It reads the setting once at startup: changing it takes effect at the next start.
- **`MainWindow.axaml`**: a new block in the sidebar's bottom row, above the theme switch, bound to `UpdateNotice.HasUpdate`, `Text`, `ButtonName`, `OpenCommand` and `OpenError`.

## Tests

- **`AppVersion`** (Core.Tests): `0.1.0`, `v0.2.0`, `0.1.0+1a00b0e`, `0.0.0-ci.44` (pre-release), `1.2.3-rc.1+abc`; rejected: `""`, `0.1`, `0.1.0.0`, `a.b.c`, `v`, `0.1.0-`, leading or trailing spaces; ordering `0.1.0 < 0.1.1 < 0.2.0 < 0.10.0 < 1.0.0`, and `0.1.0` equals `0.1.0+other`.
- **`UpdateChecker`** (Core.Tests), with a fake `HttpMessageHandler` that records requests:
  - a newer tag gives `UpdateInfo` with the built page address; the same, an older and a pre-release tag give null;
  - 404, 403, 500, a body that is not JSON, JSON without `tag_name`, a tag that is not a version, and a timeout each give null and log once;
  - a pre-release current version sends no request;
  - the request goes to the right URL with the `Accept` header;
  - an answer with an unknown `charset=` in its Content-Type is still read;
  - `html_url` pointing elsewhere is ignored: the page address is still the built one.
- **Settings** (Core.Tests): a file without `checkForUpdates` loads as `true`; `false` round-trips; saving the theme keeps `false`.
- **`UpdateNoticeViewModel`** (App.Tests): no update means hidden and the command disabled; a found update sets the texts and enables the command, which passes the page address to the `open` delegate; a null current version makes no check; a failing checker keeps the notice hidden; an `open` delegate that throws sets `OpenError` and nothing escapes.
- **Visual:** a headless screenshot of the window with the notice, in light and dark, attached to the PR.

## Criterion

1. Unit tests pass on Windows and Linux CI.
2. Manual, Windows: a local build stamped `0.0.1` (`dotnet run -p:Version=0.0.1`), while `v0.1.0` is the latest release, shows "PS5Kitsune 0.1.0" in the sidebar, and Download opens the v0.1.0 page.
3. Manual: the same build with `"checkForUpdates": false` shows nothing; with the network off, shows nothing and logs one line.
4. After merge, 0.2.0 is released with `docs/releasing.md`. The notice itself is then first seen for real when 0.3.0 comes out.
