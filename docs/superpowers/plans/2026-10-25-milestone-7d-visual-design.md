# Milestone 7d — Visual design Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every screen one design in light and dark ("Kitsune warm", Inter, shared tokens and components), add a sidebar with Home, Library and a theme switch, make the Library an icon-first grid, make every screen usable by keyboard alone with selected states exposed to screen readers, and replace the Game page's empty log box during downloads.

**Architecture:**
- **Theme:** Fluent stays for the stock controls. `Styles/AppTheme.axaml` holds:
  - the token brushes, in `ThemeDictionaries` for Light and Dark;
  - Fluent's palettes;
  - type, card, footer, nav and focus-ring styles;
  - two ControlThemes: `OptionCard` (a `RadioButton`) and `StatusChip`.
- **Shell:** `MainWindowViewModel` drives the sidebar.
  - It locks while a page runs work.
  - Otherwise it leaves every page in the navigator's stack through a new `ILeavable`, then calls `GoHome(target)`.
- **Theme choice:** `ThemeService` applies and saves `AppSettings.Theme`.
- **Keyboard:** handled by Avalonia's own controls, plus two small attached helpers: `ArrowSelection` for radio groups and `InitialFocus` for each page's first focus.

**Tech Stack:** .NET 10, C#, Avalonia 12.1.4 (compiled bindings, Fluent), CommunityToolkit.Mvvm, xUnit v3 4.0.2 on Microsoft.Testing.Platform.
- New shipped package: `Avalonia.Fonts.Inter` 12.1.4 (MIT; the font is OFL 1.1).
- New test-only packages: `Avalonia.Headless` 12.1.4 and `Avalonia.Skia` 12.1.4.

**Spec:** `docs/superpowers/specs/2026-10-25-milestone-7d-visual-design-design.md` (revision 6fc1a55).

## Global Constraints

**Language and build**
- Code, docs, commit messages and UI text in English.
- `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`: 0 warnings.
  - Use `[LoggerMessage]` for logging (CA1848).
  - Use InvariantCulture when formatting numbers (CA1305).
  - No `ToLowerInvariant` (CA1308).
  - Constant texts on view models are get-only properties with an initializer (CA1822).
- Avalonia compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault`). Every `DataTemplate` and style that binds needs `x:DataType`.

**Packages**
- `Avalonia.Fonts.Inter` 12.1.4 is the only new shipped package.
- `Avalonia.Headless` 12.1.4 and `Avalonia.Skia` 12.1.4 go in `PS5Kitsune.App.Tests` only.
- Do **not** use `Avalonia.Headless.XUnit`: it fails discovery under xunit.v3 4.0.2 with `MissingMethodException` (checked 2026-10-25).

**Colors**
- Tokens: every color in the app comes from a token key in `Styles/AppTheme.axaml`. No color literal (`#…`, `White`, `Black`) may appear in `src/PS5Kitsune.App/Views/*.axaml` (a test enforces this from Task 7).
- Token values (brushes), with Dark first and Light second:

  | Key | Dark | Light |
  |---|---|---|
  | `Surface.Page` | `#1E1A18` | `#FAF5EE` |
  | `Surface.Sidebar` | `#191614` | `#F3ECE2` |
  | `Surface.Raised` | `#2A2421` | `#FFFFFF` |
  | `Border.Subtle` | `#3A322D` | `#EADFD2` |
  | `Border.Strong` | `#5A4E46` | `#CDBFAF` |
  | `Text.Primary` | `#F2E9E1` | `#2A211C` |
  | `Text.Muted` | `#B3A69C` | `#6E6058` |
  | `Text.Title` | `#F07A5A` | `#B8381F` |
  | `Accent` | `#C73E24` | `#C73E24` |
  | `Accent.Hover` | `#B23520` | `#B23520` |
  | `OnAccent` | `#FFFFFF` | `#FFFFFF` |
  | `Error` | `#F0806A` | `#B3261E` |
  | `Warning` | `#E2A24A` | `#8A5300` |
  | `Status.Green` | `#3E6B3A` | `#2F6B2A` |
  | `Status.Orange` | `#A0661A` | `#94590E` |
  | `Status.Red` | `#B03A22` | `#B3261E` |
  | `Status.Reconvert` | `#8A5A10` | `#8A5300` |
  | `Status.Running` | `#3B6EA8` | `#2D5F96` |

**Type and spacing**
- Type classes:
  - `TextBlock.h1`: 22 SemiBold `Text.Title`
  - `.h2`: 16 SemiBold
  - body: 13 (the window's `FontSize`)
  - `.caption`: 11.5 `Text.Muted`
  - `.mono`: 12, `Cascadia Mono,Consolas,monospace`
  - `.error`: `Error`
  - `.warning`: `Warning`
- Radii are 4 (chips, inputs), 6 (buttons) and 8 (cards). The space scale is 4, 8, 12, 16, 24 and 32.

**UI texts, verbatim**
- Sidebar:
  - "Home", "Library", "PS5Kitsune";
  - the disabled tooltip "Finish or cancel the current step first.";
  - the theme radio automation names "System theme", "Light theme", "Dark theme", with the glyphs "◐", "☀", "☾".
- Library:
  - the empty texts "No games yet. Add one to start." and "Install AnyPS5 from Home first, then add games.";
  - the "…" automation name "Actions for {Name}";
  - the item automation name "{Name}, {TitleId}, {StatusText}", or "{Name}, {TitleId}" with no status;
  - the statuses "Running", "Needs reconvert", "Red", "Orange", "Green".
- Game page: "The game's output appears here.", and the progress names "Download progress" and "Checking".
- Home: the "This PC" section, with the labels "Platform", "Data folder" and "AnyPS5 profile".
- Logs:
  - "Could not save the theme." (Warning, with the exception);
  - "Could not leave {Page}." (Warning, with the exception).

**Settings JSON:** `"theme": "system" | "light" | "dark"`. It is optional, and absent means `system`. The schema version stays 1.

**Build and tests**
- Build: `dotnet build PS5Kitsune.slnx -c Release`.
- Tests: `dotnet test --solution PS5Kitsune.slnx -c Release`.
- One class: `dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "<full class name>"`. A filtered run exits with code 8 when it selects nothing, so check the summary.
- Run commands through the Bash tool. The test output is in French: "échec" = failed, "réussi" = passed, "ignoré" = skipped.
- Close any running `PS5Kitsune.exe` before building the App, since it locks `bin\Release`. Ask the user to close it; never kill it.

**Repository rules**
- Line endings: keep each edited file's existing endings. Most C# and AXAML files are CRLF; never mix LF lines into a CRLF file. New files may be LF.
- Commit locally after each task, as author La-Kiks, with a message ending in `Co-Authored-By: <your model name> <noreply@anthropic.com>`. Do not push. Never add or run an `install.cmd`.
- CI runs the App tests on Ubuntu too. Headless tests must not depend on Windows fonts: Inter is embedded, and `mono` falls back to `monospace`.

## Refinements of the spec

1. **Headless tests:**
   - `Avalonia.Headless.XUnit` is incompatible with xunit.v3 4.0.2.
   - Tests use `Avalonia.Headless` with one `HeadlessUnitTestSession` per test run, shared through a collection fixture and never disposed. `Session.Dispose()` hangs, while the process exits cleanly without it (checked 2026-10-25).
   - The test application is a test-only `HeadlessTestApp` that loads `avares://PS5Kitsune/Styles/AppTheme.axaml`. The real `App`'s composition root touches the real settings and data root.
2. **Inter is the default font family:**
   - Set with `.WithInterFont().With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })`, through `AppFonts.UseAppFonts(this AppBuilder)`.
   - This applies to flyouts and tooltips as well as windows, unlike a `Window` style.
   - `fonts:Inter#Inter` resolves to "Inter" (checked 2026-10-25).
3. **Dark `Accent`:** `#D9452A` gives 4.34:1 with white text, which fails AA.
   - `Accent` is `#C73E24` (5.07:1) and `Accent.Hover` is `#B23520` (6.13:1), in both variants.
   - As a focus ring, `#C73E24` reaches 3.4:1 on the dark page and 4.7:1 on the light page (the non-text minimum is 3:1).
   - All other spec values pass. Task 9 updates the spec's table.
4. **Tokens are brushes only** (no separate `Color` resources). Fluent's palette repeats the colors as literals in `AppTheme.axaml`.
5. **`OptionCard` is a `ControlTheme` for `RadioButton`** (key `OptionCard`), not a subclass.
   - The arrow keys come from a new attached behavior, `ArrowSelection.IsEnabled` on the parent `Panel`. It moves focus *and* checks the next enabled radio button.
   - The sidebar's theme switch uses the same behavior, with three `RadioButton`s styled with Fluent's `ToggleButton` theme.
6. **`StatusKind` is `Neutral, Green, Orange, Red, Reconvert, Running`:**
   - Check statuses map Ok → Green, Info → Running (blue), Warning → Orange, Blocking → Red, and pending → Neutral.
   - Tools map Installed → Green and Missing → Neutral.
   - `Neutral` is `Border.Strong` with `Text.Primary` text. `CheckStatusBrushConverter` is replaced by `CheckStatusKindConverter`.
7. **`ILeavable.LeaveAsync()` returns `Task<bool>`:**
   - The Settings page asks "Discard changes?" as its Back does today, and declining keeps the page.
   - The sidebar leaves **every** page in the navigator's stack, current first. A Game page under its Settings page then stops following too, instead of following forever from the history.
   - A prepared game is released only for the current page, as closing the window does.
8. **First focus:**
   - Set by an attached `InitialFocus.Rank` (1 = first choice; 0 = not a candidate).
   - Applied by a `PageView` base class (`UserControl`) when the view is loaded: the lowest-ranked visible, enabled candidate gets focus.
   - No view model event.
9. **Library:**
   - The sidebar now reaches the Library before AnyPS5 is installed, so `AddGameCommand` requires an install record (`CanAddGame`) and the empty text says why.
   - The search box is hidden while the library is empty.
   - The "…" button is not focusable (the Menu key and Shift+F10 open the same flyout).
   - There is no right-click menu.
10. **`MainWindowViewModel`:**
    - It drops the header (`DataRoot`, `Profile`, `Platform`) and its `SessionHost` and `IPlatform` parameters.
    - Its constructor becomes `(Navigator navigator, IDialogService dialogs, IPages pages, ThemeService theme, Func<ILogger>? loggerSource = null)`.
11. **Game page:**
    - The header's `StatusText` line is hidden while preparing, because the preparing card shows it.
    - The bar binds `DownloadValue` (`double`, 0 when unknown) and `IsDownloadIndeterminate`.
12. **Add result:** the verdict becomes a `StatusChip` (`VerdictKind`, `VerdictLabel`) followed by `VerdictText` in SemiBold.

## File map

**Core**
- Modify `src/PS5Kitsune.Core/Settings/AppSettings.cs`: `AppTheme`, and `Theme` on the record.

**App, new files**
- `src/PS5Kitsune.App/Styles/AppTheme.axaml`: tokens, Fluent palettes, styles and ControlThemes.
- `src/PS5Kitsune.App/Shell/AppFonts.cs`: `UseAppFonts`.
- `src/PS5Kitsune.App/Shell/ThemeService.cs`.
- `src/PS5Kitsune.App/Controls/StatusKind.cs`, `StatusChip.cs`, `ArrowSelection.cs`, `InitialFocus.cs`.
- `src/PS5Kitsune.App/Views/PageView.cs`.
- `src/PS5Kitsune.App/Navigation/ILeavable.cs`.
- `src/PS5Kitsune.App/Converters/CheckStatusKindConverter.cs`, replacing `CheckStatusBrushConverter.cs`.

**App, modified**
- `App.axaml`, `App.axaml.cs`, `Program.cs`, `PS5Kitsune.App.csproj`.
- `Navigation/Navigator.cs`, `Shell/AppLog.cs`, `Shell/PageFactory.cs`.
- View models: `MainWindowViewModel`, `HomeViewModel`, `LibraryViewModel`, `GameTileViewModel`, `GameViewModel`, `GameSettingsViewModel`, `ModeViewModel`, `ToolRowViewModel`, `AddResultViewModel`.
- All 15 views in `Views/`, plus their code-behinds.

**Tests**
- `tests/PS5Kitsune.Core.Tests/Settings/AppSettingsStoreTests.cs`.
- `tests/PS5Kitsune.App.Tests/`:
  - new: `Headless/HeadlessSession.cs`, `Headless/PageGallery.cs`, `ThemeTests.cs`, `ThemeServiceTests.cs`, `ControlsTests.cs`, `ShellTests.cs`, `LibraryViewTests.cs`, `FocusTests.cs`, `Screenshots.cs`;
  - modified: `MainWindowViewModelTests.cs`, `HomeViewModelTests.cs`, `LibraryViewModelTests.cs`, `GameViewModelTests.cs`, `GameSettingsViewModelTests.cs`, `ModeViewModelTests.cs`, `NavigatorTests.cs`, `PS5Kitsune.App.Tests.csproj`.

**Other**
- `Directory.Packages.props`.
- Docs: `docs/architecture.md`, `README.md`, and the spec.

---

### Task 1: Core — the theme setting

**Files:**
- Modify: `src/PS5Kitsune.Core/Settings/AppSettings.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs` (the root change keeps the other settings)
- Test: `tests/PS5Kitsune.Core.Tests/Settings/AppSettingsStoreTests.cs`, `tests/PS5Kitsune.App.Tests/HomeViewModelTests.cs`

**Interfaces:**
- Produces: `public enum AppTheme { System, Light, Dark }` in `PS5Kitsune.Core.Settings`, and `AppSettings(int SchemaVersion, string? DataRoot, AppTheme Theme = AppTheme.System)`.

- [ ] **Step 1: Write the failing tests** (append to `AppSettingsStoreTests`)

```csharp
    [Fact]
    public async Task A_file_without_a_theme_reads_system()
    {
        Directory.CreateDirectory(_temp.Combine("settings"));
        await File.WriteAllTextAsync(_temp.Combine("settings", "settings.json"), """{ "schemaVersion": 1, "dataRoot": null }""", TestContext.Current.CancellationToken);

        var load = Store.Load();

        Assert.Null(load.Problem);
        Assert.Equal(AppTheme.System, load.Settings.Theme);
    }

    [Fact]
    public async Task The_theme_round_trips_as_a_camel_case_string()
    {
        await Store.SaveAsync(new AppSettings(AppSettings.CurrentSchemaVersion, null, AppTheme.Dark), TestContext.Current.CancellationToken);

        Assert.Contains("\"theme\": \"dark\"", await File.ReadAllTextAsync(_temp.Combine("settings", "settings.json"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(AppTheme.Dark, Store.Load().Settings.Theme);
    }

    [Fact]
    public async Task An_unknown_theme_gives_the_defaults_and_a_problem()
    {
        Directory.CreateDirectory(_temp.Combine("settings"));
        await File.WriteAllTextAsync(_temp.Combine("settings", "settings.json"), """{ "schemaVersion": 1, "theme": "purple" }""", TestContext.Current.CancellationToken);

        var load = Store.Load();

        Assert.Equal(AppSettings.Default, load.Settings);
        Assert.NotNull(load.Problem);
    }
```

Append to `HomeViewModelTests`, next to `Changing_the_root_saves_it_and_replaces_the_session`:

```csharp
    [Fact]
    public async Task Changing_the_root_keeps_the_theme()
    {
        await _settings.SaveAsync(new AppSettings(AppSettings.CurrentSchemaVersion, null, AppTheme.Dark), TestContext.Current.CancellationToken);
        var viewModel = await CreateAsync();
        _dialogs.Folder = _temp.Combine("other");

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.Equal(new AppSettings(AppSettings.CurrentSchemaVersion, _temp.Combine("other"), AppTheme.Dark), _settings.Load().Settings);
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`. Expected: compile errors, `AppTheme` does not exist.

- [ ] **Step 3: Implement**

`src/PS5Kitsune.Core/Settings/AppSettings.cs`, replacing the record:

```csharp
/// <summary>Contents of settings.json, shared by the app and the CLI.</summary>
/// <param name="SchemaVersion">Version of the file format.</param>
/// <param name="DataRoot">The data root chosen in the app; null for the platform's default.</param>
/// <param name="Theme">The app's theme; absent in older files, which means <see cref="AppTheme.System"/>.</param>
public sealed record AppSettings(int SchemaVersion, string? DataRoot, AppTheme Theme = AppTheme.System)
{
    public const int CurrentSchemaVersion = 1;

    public static AppSettings Default { get; } = new(CurrentSchemaVersion, null);
}

/// <summary>The app's light or dark look; System follows the operating system.</summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}
```

`JsonFiles.Options` already serializes enums as camelCase strings, and an unknown name throws `JsonException`, which `Load` turns into the defaults plus a problem.

In `HomeViewModel.ChangeRootAsync` (around line 226), replace the save:

```csharp
            // Load, then change only the root: the file also holds the theme.
            await settings.SaveAsync(settings.Load().Settings with { DataRoot = root }).ConfigureAwait(true);
```

- [ ] **Step 4: Run the tests**

Run:
- `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.Settings.AppSettingsStoreTests"`
- `dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "PS5Kitsune.App.Tests.HomeViewModelTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Settings/AppSettings.cs src/PS5Kitsune.App/ViewModels/HomeViewModel.cs tests/PS5Kitsune.Core.Tests/Settings/AppSettingsStoreTests.cs tests/PS5Kitsune.App.Tests/HomeViewModelTests.cs
git commit -m "Save the app's theme in settings.json"
```

---

### Task 2: App — theme, tokens, Inter and the headless test harness

**Files:**
- Modify: `Directory.Packages.props`, `src/PS5Kitsune.App/PS5Kitsune.App.csproj`, `tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj`
- Create: `src/PS5Kitsune.App/Styles/AppTheme.axaml`, `src/PS5Kitsune.App/Shell/AppFonts.cs`, `src/PS5Kitsune.App/Shell/ThemeService.cs`
- Modify: `src/PS5Kitsune.App/App.axaml`, `src/PS5Kitsune.App/App.axaml.cs`, `src/PS5Kitsune.App/Program.cs`, `src/PS5Kitsune.App/Shell/AppLog.cs`
- Create tests: `tests/PS5Kitsune.App.Tests/Headless/HeadlessSession.cs`, `tests/PS5Kitsune.App.Tests/ThemeTests.cs`, `tests/PS5Kitsune.App.Tests/ThemeServiceTests.cs`

**Interfaces:**
- Consumes: `AppTheme`, `AppSettings.Theme` (Task 1).
- Produces:
  - `AppFonts.UseAppFonts(this AppBuilder)`;
  - `ThemeService(AppSettingsStore store, Action<AppTheme> apply, Func<ILogger>? loggerSource = null)` with `AppTheme Current`, `void Apply(AppTheme)`, `Task ChangeAsync(AppTheme)` and `static ThemeVariant VariantOf(AppTheme)`;
  - the token keys and style classes listed in Global Constraints;
  - the `OptionCard` ControlTheme key;
  - the test `HeadlessSession` fixture (collection `"Headless"`) with `Task RunAsync(Action)` and `Task<T> RunAsync<T>(Func<T>)`.

- [ ] **Step 1: Add the packages**

`Directory.Packages.props`:
- In the shipped group, add `<PackageVersion Include="Avalonia.Fonts.Inter" Version="12.1.4" />`.
- In the tests group, add `<PackageVersion Include="Avalonia.Headless" Version="12.1.4" />` and `<PackageVersion Include="Avalonia.Skia" Version="12.1.4" />`.

`src/PS5Kitsune.App/PS5Kitsune.App.csproj`: add `<PackageReference Include="Avalonia.Fonts.Inter" />`.

`tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj`: add `<PackageReference Include="Avalonia.Headless" />` and `<PackageReference Include="Avalonia.Skia" />` next to `xunit.v3`.

- [ ] **Step 2: Write the headless fixture and the failing theme tests**

`tests/PS5Kitsune.App.Tests/Headless/HeadlessSession.cs`:

```csharp
using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using PS5Kitsune.App.Shell;

namespace PS5Kitsune.App.Tests.Headless;

/// <summary>The app's styles without its composition root, which reads the real settings and data root.</summary>
public sealed class HeadlessTestApp : Application
{
    public override void Initialize() =>
        Styles.Add(new StyleInclude(new Uri("avares://PS5Kitsune.App.Tests/")) { Source = new Uri("avares://PS5Kitsune/Styles/AppTheme.axaml") });

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>()
            .UseSkia()
            .UseAppFonts()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// One Avalonia UI thread for the whole test run. It is never disposed: HeadlessUnitTestSession.Dispose hangs, and the session ends with
/// the test process.
/// </summary>
public sealed class HeadlessSession
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApp));

    public Task RunAsync(Action action) => _session.Dispatch(action, TestContext.Current.CancellationToken);

    public Task<T> RunAsync<T>(Func<T> action) => _session.Dispatch(action, TestContext.Current.CancellationToken);
}

[CollectionDefinition("Headless")]
public sealed class HeadlessCollection : ICollectionFixture<HeadlessSession>;
```

`tests/PS5Kitsune.App.Tests/ThemeTests.cs`:

```csharp
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using PS5Kitsune.App.Tests.Headless;

namespace PS5Kitsune.App.Tests;

[Collection("Headless")]
public sealed class ThemeTests(HeadlessSession session)
{
    public static TheoryData<string> Variants => ["Light", "Dark"];

    // Text and its background; the WCAG AA minimum for body text is 4.5:1.
    private static readonly (string Text, string Background)[] TextPairs =
    [
        ("Text.Primary", "Surface.Page"), ("Text.Muted", "Surface.Page"), ("Text.Title", "Surface.Page"),
        ("Text.Primary", "Surface.Raised"), ("Text.Muted", "Surface.Raised"), ("Text.Title", "Surface.Raised"),
        ("Text.Primary", "Surface.Sidebar"), ("Text.Muted", "Surface.Sidebar"),
        ("Error", "Surface.Page"), ("Warning", "Surface.Page"),
        ("OnAccent", "Accent"), ("OnAccent", "Accent.Hover"),
        ("OnAccent", "Status.Green"), ("OnAccent", "Status.Orange"), ("OnAccent", "Status.Red"),
        ("OnAccent", "Status.Reconvert"), ("OnAccent", "Status.Running"),
        ("Text.Primary", "Border.Strong"),
    ];

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task Every_text_pair_reaches_AA(string variant)
    {
        var failures = await session.RunAsync(() =>
        {
            var theme = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            return TextPairs
                .Select(pair => (pair, ratio: Contrast(Color(pair.Text, theme), Color(pair.Background, theme))))
                .Where(result => result.ratio < 4.5)
                .Select(result => $"{result.pair.Text} on {result.pair.Background}: {result.ratio:0.00}")
                .ToList();
        });

        Assert.Empty(failures);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task The_accent_is_visible_as_a_focus_ring_on_the_page(string variant)
    {
        var ratio = await session.RunAsync(() =>
        {
            var theme = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            return Contrast(Color("Accent", theme), Color("Surface.Page", theme));
        });

        // WCAG non-text contrast.
        Assert.True(ratio >= 3, $"{ratio:0.00}");
    }

    [Fact]
    public async Task Inter_is_the_default_font()
    {
        var name = await session.RunAsync(() => FontManager.Current.DefaultFontFamily.Name);

        Assert.Equal("fonts:Inter#Inter", name);
    }

    private static Color Color(string key, ThemeVariant theme) =>
        Application.Current!.TryGetResource(key, theme, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key} in {theme}.");

    private static double Contrast(Color a, Color b)
    {
        var (first, second) = (Luminance(a), Luminance(b));
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double Luminance(Color color) => 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);

    private static double Channel(byte value)
    {
        var c = value / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
```

`tests/PS5Kitsune.App.Tests/ThemeServiceTests.cs`:

```csharp
using Avalonia.Styling;
using PS5Kitsune.App.Shell;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

public sealed class ThemeServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly List<AppTheme> _applied = [];

    [Fact]
    public async Task Changing_applies_then_saves_and_keeps_the_root()
    {
        var store = new AppSettingsStore(_temp.Combine("settings"));
        await store.SaveAsync(new AppSettings(AppSettings.CurrentSchemaVersion, _temp.Combine("root")), TestContext.Current.CancellationToken);
        var service = new ThemeService(store, _applied.Add);

        await service.ChangeAsync(AppTheme.Light);

        Assert.Equal([AppTheme.Light], _applied);
        Assert.Equal(AppTheme.Light, service.Current);
        Assert.Equal(new AppSettings(AppSettings.CurrentSchemaVersion, _temp.Combine("root"), AppTheme.Light), store.Load().Settings);
    }

    [Fact]
    public async Task A_failed_save_still_applies_the_theme()
    {
        // A file where the settings folder should be: the save cannot create it.
        await File.WriteAllTextAsync(_temp.Combine("settings"), "x", TestContext.Current.CancellationToken);
        var service = new ThemeService(new AppSettingsStore(_temp.Combine("settings")), _applied.Add);

        await service.ChangeAsync(AppTheme.Dark);

        Assert.Equal([AppTheme.Dark], _applied);
        Assert.Equal(AppTheme.Dark, service.Current);
    }

    [Theory]
    [InlineData(AppTheme.System, "Default")]
    [InlineData(AppTheme.Light, "Light")]
    [InlineData(AppTheme.Dark, "Dark")]
    public void Each_theme_maps_to_its_variant(AppTheme theme, string variant) =>
        Assert.Equal(variant, ThemeService.VariantOf(theme).Key.ToString());

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`. Expected: compile errors (`UseAppFonts`, `ThemeService` missing).

- [ ] **Step 4: Implement the fonts, the theme service and the log message**

`src/PS5Kitsune.App/Shell/AppFonts.cs`:

```csharp
using Avalonia;
using Avalonia.Media;

namespace PS5Kitsune.App.Shell;

public static class AppFonts
{
    /// <summary>Embeds Inter and makes it the default family, so windows, flyouts and tooltips all use it on every OS.</summary>
    public static AppBuilder UseAppFonts(this AppBuilder builder) =>
        builder.WithInterFont().With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" });
}
```

`src/PS5Kitsune.App/Program.cs`, `BuildAvaloniaApp`:

```csharp
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseAppFonts()
            .LogToTrace();
```

(Add `using PS5Kitsune.App.Shell;`.)

`src/PS5Kitsune.App/Shell/ThemeService.cs`:

```csharp
using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.App.Shell;

/// <summary>Applies the app's theme and saves the user's choice in settings.json.</summary>
/// <param name="apply">Sets the application's theme variant; tests record it instead.</param>
public sealed class ThemeService(AppSettingsStore store, Action<AppTheme> apply, Func<ILogger>? loggerSource = null)
{
    public AppTheme Current { get; private set; }

    public void Apply(AppTheme theme)
    {
        Current = theme;
        apply(theme);
    }

    /// <summary>Applies at once, then saves; a failed save is logged and the theme stays for this run.</summary>
    public async Task ChangeAsync(AppTheme theme)
    {
        Apply(theme);
        try
        {
            await store.SaveAsync(store.Load().Settings with { Theme = theme }).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            loggerSource?.Invoke().ThemeSaveFailed(exception);
        }
    }

    public static ThemeVariant VariantOf(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
```

`src/PS5Kitsune.App/Shell/AppLog.cs`, add:

```csharp
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save the theme.")]
    public static partial void ThemeSaveFailed(this ILogger logger, Exception exception);
```

(`AppLog` is `internal`. `ThemeService` is in the same assembly, so this is fine.)

- [ ] **Step 5: Write the theme file**

`src/PS5Kitsune.App/Styles/AppTheme.axaml`. The token values are copied verbatim from Global Constraints. The Fluent palettes repeat them.

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <!-- Every color of the app lives here; views use these keys. Values: docs/superpowers/specs/2026-10-25-milestone-7d-visual-design-design.md. -->
  <Styles.Resources>
    <ResourceDictionary>
      <ResourceDictionary.ThemeDictionaries>
        <ResourceDictionary x:Key="Dark">
          <SolidColorBrush x:Key="Surface.Page" Color="#1E1A18" />
          <SolidColorBrush x:Key="Surface.Sidebar" Color="#191614" />
          <SolidColorBrush x:Key="Surface.Raised" Color="#2A2421" />
          <SolidColorBrush x:Key="Border.Subtle" Color="#3A322D" />
          <SolidColorBrush x:Key="Border.Strong" Color="#5A4E46" />
          <SolidColorBrush x:Key="Text.Primary" Color="#F2E9E1" />
          <SolidColorBrush x:Key="Text.Muted" Color="#B3A69C" />
          <SolidColorBrush x:Key="Text.Title" Color="#F07A5A" />
          <SolidColorBrush x:Key="Accent" Color="#C73E24" />
          <SolidColorBrush x:Key="Accent.Hover" Color="#B23520" />
          <SolidColorBrush x:Key="OnAccent" Color="#FFFFFF" />
          <SolidColorBrush x:Key="Error" Color="#F0806A" />
          <SolidColorBrush x:Key="Warning" Color="#E2A24A" />
          <SolidColorBrush x:Key="Status.Green" Color="#3E6B3A" />
          <SolidColorBrush x:Key="Status.Orange" Color="#A0661A" />
          <SolidColorBrush x:Key="Status.Red" Color="#B03A22" />
          <SolidColorBrush x:Key="Status.Reconvert" Color="#8A5A10" />
          <SolidColorBrush x:Key="Status.Running" Color="#3B6EA8" />
        </ResourceDictionary>
        <ResourceDictionary x:Key="Light">
          <SolidColorBrush x:Key="Surface.Page" Color="#FAF5EE" />
          <SolidColorBrush x:Key="Surface.Sidebar" Color="#F3ECE2" />
          <SolidColorBrush x:Key="Surface.Raised" Color="#FFFFFF" />
          <SolidColorBrush x:Key="Border.Subtle" Color="#EADFD2" />
          <SolidColorBrush x:Key="Border.Strong" Color="#CDBFAF" />
          <SolidColorBrush x:Key="Text.Primary" Color="#2A211C" />
          <SolidColorBrush x:Key="Text.Muted" Color="#6E6058" />
          <SolidColorBrush x:Key="Text.Title" Color="#B8381F" />
          <SolidColorBrush x:Key="Accent" Color="#C73E24" />
          <SolidColorBrush x:Key="Accent.Hover" Color="#B23520" />
          <SolidColorBrush x:Key="OnAccent" Color="#FFFFFF" />
          <SolidColorBrush x:Key="Error" Color="#B3261E" />
          <SolidColorBrush x:Key="Warning" Color="#8A5300" />
          <SolidColorBrush x:Key="Status.Green" Color="#2F6B2A" />
          <SolidColorBrush x:Key="Status.Orange" Color="#94590E" />
          <SolidColorBrush x:Key="Status.Red" Color="#B3261E" />
          <SolidColorBrush x:Key="Status.Reconvert" Color="#8A5300" />
          <SolidColorBrush x:Key="Status.Running" Color="#2D5F96" />
        </ResourceDictionary>
      </ResourceDictionary.ThemeDictionaries>

      <!-- Fluent's accent buttons: our accent with white text in both variants (Fluent's dark accent button uses black text). -->
      <StaticResource x:Key="AccentButtonBackground" ResourceKey="Accent" />
      <StaticResource x:Key="AccentButtonBackgroundPointerOver" ResourceKey="Accent.Hover" />
      <StaticResource x:Key="AccentButtonBackgroundPressed" ResourceKey="Accent.Hover" />
      <StaticResource x:Key="AccentButtonForeground" ResourceKey="OnAccent" />
      <StaticResource x:Key="AccentButtonForegroundPointerOver" ResourceKey="OnAccent" />
      <StaticResource x:Key="AccentButtonForegroundPressed" ResourceKey="OnAccent" />

      <CornerRadius x:Key="Radius.Small">4</CornerRadius>
      <CornerRadius x:Key="Radius.Medium">6</CornerRadius>
      <CornerRadius x:Key="Radius.Large">8</CornerRadius>
      <x:Double x:Key="Space.1">4</x:Double>
      <x:Double x:Key="Space.2">8</x:Double>
      <x:Double x:Key="Space.3">12</x:Double>
      <x:Double x:Key="Space.4">16</x:Double>
      <x:Double x:Key="Space.5">24</x:Double>
      <x:Double x:Key="Space.6">32</x:Double>

      <!-- A selectable card: Mode's install choices. A RadioButton, so screen readers hear "selected" and arrow keys work (ArrowSelection). -->
      <ControlTheme x:Key="OptionCard" TargetType="RadioButton">
        <Setter Property="Background" Value="{DynamicResource Surface.Raised}" />
        <Setter Property="Foreground" Value="{DynamicResource Text.Primary}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Border.Subtle}" />
        <Setter Property="BorderThickness" Value="2" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="Padding" Value="20" />
        <Setter Property="HorizontalAlignment" Value="Stretch" />
        <Setter Property="VerticalAlignment" Value="Stretch" />
        <Setter Property="HorizontalContentAlignment" Value="Left" />
        <Setter Property="VerticalContentAlignment" Value="Top" />
        <Setter Property="Template">
          <ControlTemplate>
            <Panel>
              <Border Name="PART_Border" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                      BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="{TemplateBinding CornerRadius}" Padding="{TemplateBinding Padding}">
                <ContentPresenter Name="PART_ContentPresenter" Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}"
                                  HorizontalContentAlignment="{TemplateBinding HorizontalContentAlignment}"
                                  VerticalContentAlignment="{TemplateBinding VerticalContentAlignment}" />
              </Border>
              <Border Name="PART_Check" Width="22" Height="22" CornerRadius="11" Background="{DynamicResource Accent}"
                      HorizontalAlignment="Right" VerticalAlignment="Top" Margin="0,10,10,0" IsVisible="False">
                <TextBlock Text="✓" Foreground="{DynamicResource OnAccent}" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center" />
              </Border>
            </Panel>
          </ControlTemplate>
        </Setter>
        <Style Selector="^:pointerover">
          <Setter Property="BorderBrush" Value="{DynamicResource Border.Strong}" />
        </Style>
        <Style Selector="^:checked">
          <Setter Property="BorderBrush" Value="{DynamicResource Accent}" />
        </Style>
        <Style Selector="^:checked /template/ Border#PART_Check">
          <Setter Property="IsVisible" Value="True" />
        </Style>
        <Style Selector="^:disabled">
          <Setter Property="Opacity" Value="0.5" />
        </Style>
      </ControlTheme>
    </ResourceDictionary>
  </Styles.Resources>

  <FluentTheme>
    <FluentTheme.Palettes>
      <ColorPaletteResources x:Key="Dark" Accent="#C73E24" RegionColor="#1E1A18" AltHigh="#1E1A18" BaseHigh="#F2E9E1"
                             ChromeAltLow="#2A2421" ChromeLow="#2A2421" ChromeMediumLow="#2A2421" ChromeMedium="#3A322D" ListLow="#3A322D" />
      <ColorPaletteResources x:Key="Light" Accent="#C73E24" RegionColor="#FAF5EE" AltHigh="#FAF5EE" BaseHigh="#2A211C"
                             ChromeAltLow="#FFFFFF" ChromeLow="#FFFFFF" ChromeMediumLow="#FFFFFF" ChromeMedium="#EADFD2" ListLow="#EADFD2" />
    </FluentTheme.Palettes>
  </FluentTheme>

  <!-- Type -->
  <Style Selector="TextBlock.h1">
    <Setter Property="FontSize" Value="22" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Foreground" Value="{DynamicResource Text.Title}" />
    <Setter Property="TextWrapping" Value="Wrap" />
  </Style>
  <Style Selector="TextBlock.h2">
    <Setter Property="FontSize" Value="16" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="TextWrapping" Value="Wrap" />
  </Style>
  <Style Selector="TextBlock.caption">
    <Setter Property="FontSize" Value="11.5" />
    <Setter Property="Foreground" Value="{DynamicResource Text.Muted}" />
    <Setter Property="TextWrapping" Value="Wrap" />
  </Style>
  <Style Selector=":is(TextBlock).mono">
    <Setter Property="FontFamily" Value="Cascadia Mono,Consolas,monospace" />
    <Setter Property="FontSize" Value="12" />
    <Setter Property="TextWrapping" Value="Wrap" />
  </Style>
  <Style Selector="TextBlock.error">
    <Setter Property="Foreground" Value="{DynamicResource Error}" />
    <Setter Property="TextWrapping" Value="Wrap" />
  </Style>
  <Style Selector="TextBlock.warning">
    <Setter Property="Foreground" Value="{DynamicResource Warning}" />
    <Setter Property="TextWrapping" Value="Wrap" />
  </Style>

  <!-- Surfaces -->
  <Style Selector="Border.card">
    <Setter Property="Background" Value="{DynamicResource Surface.Raised}" />
    <Setter Property="BorderBrush" Value="{DynamicResource Border.Subtle}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="CornerRadius" Value="8" />
    <Setter Property="Padding" Value="16" />
  </Style>
  <Style Selector="Border.card.error">
    <Setter Property="BorderBrush" Value="{DynamicResource Error}" />
  </Style>
  <Style Selector="Border.log">
    <Setter Property="Background" Value="{DynamicResource Surface.Raised}" />
    <Setter Property="CornerRadius" Value="8" />
    <Setter Property="Padding" Value="8" />
  </Style>
  <Style Selector="Border.footer">
    <Setter Property="BorderBrush" Value="{DynamicResource Border.Subtle}" />
    <Setter Property="BorderThickness" Value="0,1,0,0" />
    <Setter Property="Padding" Value="0,12,0,0" />
    <Setter Property="Margin" Value="0,16,0,0" />
  </Style>
  <Style Selector="Border.divider">
    <Setter Property="BorderBrush" Value="{DynamicResource Border.Subtle}" />
    <Setter Property="BorderThickness" Value="0,1,0,0" />
  </Style>
  <Style Selector="Button">
    <Setter Property="CornerRadius" Value="6" />
  </Style>

  <!-- Sidebar -->
  <Style Selector="Button.nav">
    <Setter Property="HorizontalAlignment" Value="Stretch" />
    <Setter Property="HorizontalContentAlignment" Value="Left" />
    <Setter Property="Padding" Value="12,8" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderThickness" Value="3,0,0,0" />
    <Setter Property="BorderBrush" Value="Transparent" />
    <Setter Property="CornerRadius" Value="4" />
    <Setter Property="ToolTip.ShowOnDisabled" Value="True" />
  </Style>
  <Style Selector="Button.nav.current">
    <Setter Property="Background" Value="{DynamicResource Surface.Raised}" />
    <Setter Property="BorderBrush" Value="{DynamicResource Accent}" />
    <Setter Property="FontWeight" Value="SemiBold" />
  </Style>

  <!-- Keyboard focus: a 2 px accent ring outside the control, shown only after keyboard focus. -->
  <Style Selector=":is(Control)">
    <Setter Property="FocusAdorner">
      <FocusAdornerTemplate>
        <Border BorderThickness="2" BorderBrush="{DynamicResource Accent}" CornerRadius="8" Margin="-4" />
      </FocusAdornerTemplate>
    </Setter>
  </Style>
</Styles>
```

If the build rejects `StaticResource` aliases inside the resource dictionary, write the six `AccentButton*` keys as `SolidColorBrush` entries inside each theme dictionary instead, with the same colors as `Accent`, `Accent.Hover` and `OnAccent`.

`src/PS5Kitsune.App/App.axaml` becomes:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="PS5Kitsune.App.App"
             RequestedThemeVariant="Default">
  <Application.Styles>
    <StyleInclude Source="/Styles/AppTheme.axaml" />
  </Application.Styles>
</Application>
```

`src/PS5Kitsune.App/App.axaml.cs`, in `OnFrameworkInitializationCompleted`:
- Right after `var loaded = settings.Load();`, create the theme service and apply the saved theme before any window exists:

```csharp
        var theme = new ThemeService(settings, choice => RequestedThemeVariant = ThemeService.VariantOf(choice), () => sessions.Current.Get<ILogger<App>>());
        theme.Apply(loaded.Settings.Theme);
```

  The lambda captures `sessions`, which is declared on the next line. Move the `theme` lines after the `sessions` declaration so it compiles.
- Keep `theme` in a local. Task 4 passes it to `MainWindowViewModel`.

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "PS5Kitsune.App.Tests.ThemeTests"`, then the same with `PS5Kitsune.App.Tests.ThemeServiceTests`.

Expected: PASS. If a contrast pair fails, report it rather than changing a token silently. The values above were computed to pass.

Then build the whole solution and run the full suite. Expected: 0 warnings, and every existing test passes.

- [ ] **Step 7: Commit**

```bash
git add Directory.Packages.props src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Add the Kitsune warm theme, Inter and a headless test session"
```

---

### Task 3: App — shared controls: status chip, arrow selection, first focus

**Files:**
- Create: `src/PS5Kitsune.App/Controls/StatusKind.cs`, `src/PS5Kitsune.App/Controls/StatusChip.cs`, `src/PS5Kitsune.App/Controls/ArrowSelection.cs`, `src/PS5Kitsune.App/Controls/InitialFocus.cs`, `src/PS5Kitsune.App/Views/PageView.cs`, `src/PS5Kitsune.App/Converters/CheckStatusKindConverter.cs`
- Modify: `src/PS5Kitsune.App/Styles/AppTheme.axaml` (the `StatusChip` ControlTheme)
- Test: `tests/PS5Kitsune.App.Tests/ControlsTests.cs`

**Interfaces:**
- Produces:
  - `enum StatusKind { Neutral, Green, Orange, Red, Reconvert, Running }` (namespace `PS5Kitsune.App.Controls`);
  - `StatusChip` (`Kind`, `Text`);
  - `ArrowSelection.IsEnabled`, attached on `Panel`;
  - `InitialFocus.Rank`, attached int on `Control`, with `static bool InitialFocus.Apply(Control root)`;
  - `PageView : UserControl`, the base of every page view;
  - `CheckStatusKindConverter.Instance` (`CheckStatus?` → `StatusKind`).
- XAML namespace: `xmlns:c="using:PS5Kitsune.App.Controls"`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.App.Tests/ControlsTests.cs`:

```csharp
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using PS5Kitsune.App.Controls;
using PS5Kitsune.App.Converters;
using PS5Kitsune.App.Tests.Headless;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.App.Tests;

[Collection("Headless")]
public sealed class ControlsTests(HeadlessSession session)
{
    [Fact]
    public Task Arrow_keys_check_the_next_enabled_radio_button_and_skip_disabled_ones() => session.RunAsync(() =>
    {
        var first = new RadioButton { Content = "A", GroupName = "g", IsChecked = true };
        var disabled = new RadioButton { Content = "B", GroupName = "g", IsEnabled = false };
        var third = new RadioButton { Content = "C", GroupName = "g" };
        var panel = new StackPanel { Children = { first, disabled, third } };
        ArrowSelection.SetIsEnabled(panel, true);
        var window = Show(panel);
        first.Focus(NavigationMethod.Tab);

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);

        Assert.True(third.IsChecked);
        Assert.False(first.IsChecked);
        Assert.True(third.IsFocused);

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);

        Assert.True(first.IsChecked);
        Assert.True(first.IsFocused);
        window.Close();
    });

    [Fact]
    public Task Initial_focus_goes_to_the_lowest_visible_enabled_rank() => session.RunAsync(() =>
    {
        var back = new Button { Content = "Back" };
        var hidden = new Button { Content = "Launch again", IsVisible = false };
        var stop = new Button { Content = "Stop" };
        InitialFocus.SetRank(back, 3);
        InitialFocus.SetRank(hidden, 1);
        InitialFocus.SetRank(stop, 2);
        var root = new StackPanel { Children = { back, hidden, stop } };
        var window = Show(root);

        Assert.True(InitialFocus.Apply(root));

        Assert.True(stop.IsFocused);
        window.Close();
    });

    [Fact]
    public Task A_status_chip_is_named_by_its_text_and_styled_by_its_kind() => session.RunAsync(() =>
    {
        var chip = new StatusChip { Kind = StatusKind.Running, Text = "Running" };
        var window = Show(chip);

        Assert.Equal("Running", AutomationProperties.GetName(chip));
        Assert.Equal([":running"], chip.ActivePseudoClasses);
        window.Close();
    });

    [Theory]
    [InlineData(CheckStatus.Ok, StatusKind.Green)]
    [InlineData(CheckStatus.Info, StatusKind.Running)]
    [InlineData(CheckStatus.Warning, StatusKind.Orange)]
    [InlineData(CheckStatus.Blocking, StatusKind.Red)]
    [InlineData(null, StatusKind.Neutral)]
    public void Check_statuses_map_to_chip_kinds(CheckStatus? status, StatusKind kind) =>
        Assert.Equal(kind, CheckStatusKindConverter.Instance.Convert(status, typeof(StatusKind), null, System.Globalization.CultureInfo.InvariantCulture));

    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }
}
```

`ActivePseudoClasses` is an internal test hook on `StatusChip` (below).

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`. Expected: compile errors.

- [ ] **Step 3: Implement**

`src/PS5Kitsune.App/Controls/StatusKind.cs`:

```csharp
namespace PS5Kitsune.App.Controls;

/// <summary>A status chip's color: compatibility (Green, Orange, Red), Reconvert, Running (also check info), or Neutral (missing, pending).</summary>
public enum StatusKind
{
    Neutral,
    Green,
    Orange,
    Red,
    Reconvert,
    Running,
}
```

`src/PS5Kitsune.App/Controls/StatusChip.cs`:

```csharp
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;

namespace PS5Kitsune.App.Controls;

/// <summary>A small colored label; its text is also its name for screen readers, so the color is never the only signal.</summary>
public sealed class StatusChip : TemplatedControl
{
    public static readonly StyledProperty<StatusKind> KindProperty = AvaloniaProperty.Register<StatusChip, StatusKind>(nameof(Kind));

    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<StatusChip, string?>(nameof(Text));

    private static readonly IReadOnlyDictionary<StatusKind, string> PseudoClassNames = new Dictionary<StatusKind, string>
    {
        [StatusKind.Neutral] = ":neutral",
        [StatusKind.Green] = ":green",
        [StatusKind.Orange] = ":orange",
        [StatusKind.Red] = ":red",
        [StatusKind.Reconvert] = ":reconvert",
        [StatusKind.Running] = ":running",
    };

    static StatusChip()
    {
        KindProperty.Changed.AddClassHandler<StatusChip>((chip, _) => chip.UpdateKind());
        TextProperty.Changed.AddClassHandler<StatusChip>((chip, _) => AutomationProperties.SetName(chip, chip.Text));
    }

    public StatusChip() => UpdateKind();

    public StatusKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The pseudo-classes set now, for tests.</summary>
    internal IEnumerable<string> ActivePseudoClasses => PseudoClassNames.Values.Where(name => PseudoClasses.Contains(name));

    private void UpdateKind()
    {
        foreach (var (kind, name) in PseudoClassNames)
        {
            PseudoClasses.Set(name, kind == Kind);
        }
    }
}
```

The test project needs `InternalsVisibleTo`. Add `<InternalsVisibleTo Include="PS5Kitsune.App.Tests" />` in an `ItemGroup` of `src/PS5Kitsune.App/PS5Kitsune.App.csproj`, unless it is already there (check first).

In `AppTheme.axaml`, inside `Styles.Resources` → `ResourceDictionary`, add `xmlns:c="using:PS5Kitsune.App.Controls"` on the root `Styles` element, and this ControlTheme:

```xml
      <ControlTheme x:Key="{x:Type c:StatusChip}" TargetType="c:StatusChip">
        <Setter Property="Background" Value="{DynamicResource Border.Strong}" />
        <Setter Property="Foreground" Value="{DynamicResource Text.Primary}" />
        <Setter Property="FontSize" Value="11" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Padding" Value="6,1" />
        <Setter Property="CornerRadius" Value="4" />
        <Setter Property="HorizontalAlignment" Value="Left" />
        <Setter Property="VerticalAlignment" Value="Top" />
        <Setter Property="Template">
          <ControlTemplate>
            <Border Background="{TemplateBinding Background}" CornerRadius="{TemplateBinding CornerRadius}" Padding="{TemplateBinding Padding}">
              <TextBlock Text="{TemplateBinding Text}" Foreground="{TemplateBinding Foreground}"
                         FontSize="{TemplateBinding FontSize}" FontWeight="{TemplateBinding FontWeight}" />
            </Border>
          </ControlTemplate>
        </Setter>
        <Style Selector="^:green">
          <Setter Property="Background" Value="{DynamicResource Status.Green}" />
          <Setter Property="Foreground" Value="{DynamicResource OnAccent}" />
        </Style>
        <Style Selector="^:orange">
          <Setter Property="Background" Value="{DynamicResource Status.Orange}" />
          <Setter Property="Foreground" Value="{DynamicResource OnAccent}" />
        </Style>
        <Style Selector="^:red">
          <Setter Property="Background" Value="{DynamicResource Status.Red}" />
          <Setter Property="Foreground" Value="{DynamicResource OnAccent}" />
        </Style>
        <Style Selector="^:reconvert">
          <Setter Property="Background" Value="{DynamicResource Status.Reconvert}" />
          <Setter Property="Foreground" Value="{DynamicResource OnAccent}" />
        </Style>
        <Style Selector="^:running">
          <Setter Property="Background" Value="{DynamicResource Status.Running}" />
          <Setter Property="Foreground" Value="{DynamicResource OnAccent}" />
        </Style>
      </ControlTheme>
```

`src/PS5Kitsune.App/Controls/ArrowSelection.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PS5Kitsune.App.Controls;

/// <summary>
/// Arrow keys inside a panel of radio buttons move to the next enabled one and check it, as Windows radio groups do. Tunnels, so it runs
/// before the radio button's own key handling.
/// </summary>
public static class ArrowSelection
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Panel, bool>("IsEnabled", typeof(ArrowSelection));

    static ArrowSelection() => IsEnabledProperty.Changed.AddClassHandler<Panel>(OnIsEnabledChanged);

    public static bool GetIsEnabled(Panel panel) => panel.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Panel panel, bool value) => panel.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(Panel panel, AvaloniaPropertyChangedEventArgs e)
    {
        panel.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        if (e.NewValue is true)
        {
            panel.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        }
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var step = e.Key switch
        {
            Key.Left or Key.Up => -1,
            Key.Right or Key.Down => 1,
            _ => 0,
        };
        if (step == 0 || sender is not Panel panel || e.Source is not Visual source
            || source.FindAncestorOfType<RadioButton>(includeSelf: true) is not { } current)
        {
            return;
        }

        var buttons = panel.Children.OfType<RadioButton>().ToList();
        for (var index = buttons.IndexOf(current) + step; index >= 0 && index < buttons.Count; index += step)
        {
            if (buttons[index] is { IsEffectivelyEnabled: true, IsVisible: true } next)
            {
                next.IsChecked = true;
                next.Focus(NavigationMethod.Directional);
                e.Handled = true;
                return;
            }
        }

        // At either end: stay, and keep the arrow from moving focus out of the group.
        e.Handled = buttons.Contains(current);
    }
}
```

`src/PS5Kitsune.App/Controls/InitialFocus.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace PS5Kitsune.App.Controls;

/// <summary>Which control gets focus when a page is shown: the lowest rank among the visible, enabled candidates (0 = not a candidate).</summary>
public static class InitialFocus
{
    public static readonly AttachedProperty<int> RankProperty = AvaloniaProperty.RegisterAttached<Control, int>("Rank", typeof(InitialFocus));

    public static int GetRank(Control control) => control.GetValue(RankProperty);

    public static void SetRank(Control control, int value) => control.SetValue(RankProperty, value);

    /// <returns>True when a candidate took focus.</returns>
    public static bool Apply(Control root) =>
        root.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => GetRank(control) > 0 && control.IsEffectivelyVisible && control.IsEffectivelyEnabled)
            .OrderBy(GetRank)
            .FirstOrDefault()
            ?.Focus() ?? false;
}
```

`src/PS5Kitsune.App/Views/PageView.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PS5Kitsune.App.Controls;

namespace PS5Kitsune.App.Views;

/// <summary>Every page's view: once loaded, focus goes to its <see cref="InitialFocus"/> candidate, so the keyboard starts on the page.</summary>
public class PageView : UserControl
{
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // After the first layout, so bindings have set IsVisible and IsEnabled.
        Dispatcher.UIThread.Post(() => InitialFocus.Apply(this), DispatcherPriority.Loaded);
    }
}
```

`src/PS5Kitsune.App/Converters/CheckStatusKindConverter.cs`:

```csharp
using System.Globalization;
using Avalonia.Data.Converters;
using PS5Kitsune.App.Controls;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.App.Converters;

/// <summary>A check's chip color; Neutral while checking (null).</summary>
public sealed class CheckStatusKindConverter : IValueConverter
{
    public static CheckStatusKindConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        CheckStatus.Ok => StatusKind.Green,
        CheckStatus.Info => StatusKind.Running,
        CheckStatus.Warning => StatusKind.Orange,
        CheckStatus.Blocking => StatusKind.Red,
        _ => StatusKind.Neutral,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

Leave `CheckStatusBrushConverter` in place: `SystemCheckView` still uses it until Task 7 deletes it.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "PS5Kitsune.App.Tests.ControlsTests"`. Expected: PASS.

If `KeyPressQwerty` does not reach the radio button because the focus is not set, call `Dispatcher.UIThread.RunJobs()` after `Focus`. Report any change to the test.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests/ControlsTests.cs
git commit -m "Add the status chip, arrow selection and first-focus helpers"
```

---

### Task 4: App — the sidebar shell and leaving pages

**Files:**
- Create: `src/PS5Kitsune.App/Navigation/ILeavable.cs`
- Modify:
  - `src/PS5Kitsune.App/Navigation/Navigator.cs` (`Pages`)
  - `src/PS5Kitsune.App/ViewModels/MainWindowViewModel.cs`
  - `src/PS5Kitsune.App/ViewModels/GameViewModel.cs` (`ILeavable`)
  - `src/PS5Kitsune.App/ViewModels/GameSettingsViewModel.cs` (`ILeavable`)
  - `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs` (`Platform`, a new `IPlatform` parameter)
  - `src/PS5Kitsune.App/ViewModels/LibraryViewModel.cs` (drop `HomeCommand`; add `CanAddGame` and `EmptyText`)
  - `src/PS5Kitsune.App/Shell/PageFactory.cs`, `src/PS5Kitsune.App/Shell/AppLog.cs`
  - `src/PS5Kitsune.App/App.axaml.cs`
  - `src/PS5Kitsune.App/Views/MainWindow.axaml`, `MainWindow.axaml.cs`
- Test:
  - `tests/PS5Kitsune.App.Tests/MainWindowViewModelTests.cs` and `NavigatorTests.cs`;
  - `HomeViewModelTests.cs` and `LibraryViewModelTests.cs` (the constructor and the removed `HomeCommand`);
  - `GameSettingsViewModelTests.cs`;
  - new `tests/PS5Kitsune.App.Tests/ShellTests.cs` (headless).

**Interfaces:**
- Consumes: `ThemeService` (Task 2), `ArrowSelection` (Task 3).
- Produces:
  - `ILeavable { Task<bool> LeaveAsync(); }`;
  - `Navigator.Pages` (`IEnumerable<object>`, current first);
  - `MainWindowViewModel(Navigator navigator, IDialogService dialogs, IPages pages, ThemeService theme, Func<ILogger>? loggerSource = null)` with:
    - `IsBusy` and `CanNavigate`;
    - `GoHomeCommand` and `GoLibraryCommand` (`IAsyncRelayCommand`);
    - `NavSection CurrentSection`, `IsHomeCurrent`, `IsLibraryCurrent`;
    - `HomeItemStatus` and `LibraryItemStatus` (`"current"` or null);
    - `NavTip`;
    - `IsSystemTheme`, `IsLightTheme`, `IsDarkTheme` (settable);
    - `Task ThemeSaving`;
    - and the unchanged `NeedsCloseHandling` and `ConfirmCloseAsync`;
  - `public enum NavSection { None, Home, Library }` (in `PS5Kitsune.App.ViewModels`);
  - `HomeViewModel(SessionHost sessions, AppSettingsStore settings, IDialogService dialogs, Navigator navigator, IPages pages, IPlatform platform)` with `string Platform`;
  - `LibraryViewModel.CanAddGame` and `EmptyText`.

- [ ] **Step 1: Write the failing tests**

`NavigatorTests`, add:

```csharp
    [Fact]
    public void Pages_lists_the_current_page_then_the_history_newest_first()
    {
        var navigator = new Navigator();
        navigator.GoTo("a");
        navigator.GoTo("b");
        navigator.GoTo("c");

        Assert.Equal(["c", "b", "a"], navigator.Pages);
    }
```

`MainWindowViewModelTests`:
- Rewrite the setup for the new constructor and remove `The_header_follows_the_data_root`; Home's tests cover the data root and profile.
- The class keeps `_sessions`, because the install and add tests use it.
- The constructor becomes:

```csharp
        _sessions = new SessionHost(root => TestSessions.Create(root, _installer), _temp.Combine("a"));
        _theme = new ThemeService(new AppSettingsStore(_temp.Combine("settings")), _applied.Add);
        _viewModel = new MainWindowViewModel(_navigator, _dialogs, new FakePages(), _theme);
```

with the fields `private readonly ThemeService _theme;` and `private readonly List<AppTheme> _applied = [];`.

Add:

```csharp
    [Fact]
    public void The_sidebar_is_locked_while_the_page_runs_work()
    {
        var work = new FakeWork { IsRunning = true };
        _navigator.GoTo(work);

        Assert.True(_viewModel.IsBusy);
        Assert.False(_viewModel.GoLibraryCommand.CanExecute(null));
        Assert.Equal("Finish or cancel the current step first.", _viewModel.NavTip);

        work.IsRunning = false;

        Assert.False(_viewModel.IsBusy);
        Assert.True(_viewModel.GoLibraryCommand.CanExecute(null));
        Assert.Null(_viewModel.NavTip);
    }

    [Fact]
    public async Task Going_to_the_library_leaves_every_page_then_clears_the_history()
    {
        var game = new FakeLeavable();
        var settings = new FakeLeavable();
        _navigator.GoTo("library");
        _navigator.GoTo(game);
        _navigator.GoTo(settings);

        await _viewModel.GoLibraryCommand.ExecuteAsync(null);

        Assert.True(settings.Left);
        Assert.True(game.Left);
        Assert.Equal("library", _navigator.CurrentPage);
        Assert.Equal(["library"], _navigator.Pages);
    }

    [Fact]
    public async Task A_page_that_refuses_to_leave_keeps_the_user_there()
    {
        var settings = new FakeLeavable { Allows = false };
        _navigator.GoTo(settings);

        await _viewModel.GoHomeCommand.ExecuteAsync(null);

        Assert.Same(settings, _navigator.CurrentPage);
    }

    [Fact]
    public async Task Leaving_a_page_that_owns_a_game_releases_it()
    {
        var owner = new FakeOwner();
        _navigator.GoTo(owner);

        await _viewModel.GoHomeCommand.ExecuteAsync(null);

        Assert.True(owner.Released);
        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_page_that_fails_to_leave_does_not_block_navigation()
    {
        _navigator.GoTo(new FakeLeavable { Throws = true });

        await _viewModel.GoHomeCommand.ExecuteAsync(null);

        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public void The_current_section_follows_the_page()
    {
        var home = new HomeViewModel(_sessions, new AppSettingsStore(_temp.Combine("settings")), _dialogs, _navigator, new FakePages(), new TestPlatform());
        _navigator.GoTo(home);
        Assert.Equal(NavSection.Home, _viewModel.CurrentSection);
        Assert.True(_viewModel.IsHomeCurrent);
        Assert.Equal("current", _viewModel.HomeItemStatus);

        _navigator.GoTo("mode");
        Assert.Equal(NavSection.None, _viewModel.CurrentSection);
        Assert.Null(_viewModel.HomeItemStatus);
    }

    [Fact]
    public async Task Choosing_a_theme_applies_and_saves_it()
    {
        Assert.True(_viewModel.IsSystemTheme);

        _viewModel.IsDarkTheme = true;
        await _viewModel.ThemeSaving;

        Assert.Equal([AppTheme.Dark], _applied);
        Assert.True(_viewModel.IsDarkTheme);
        Assert.False(_viewModel.IsSystemTheme);
        Assert.Equal(AppTheme.Dark, new AppSettingsStore(_temp.Combine("settings")).Load().Settings.Theme);
    }

    private sealed class FakeLeavable : ILeavable
    {
        public bool Allows { get; init; } = true;

        public bool Throws { get; init; }

        public bool Left { get; private set; }

        public Task<bool> LeaveAsync()
        {
            Left = true;
            return Throws ? Task.FromException<bool>(new IOException("disk gone")) : Task.FromResult(Allows);
        }
    }
```

`FakeWork` must raise `PropertyChanged`, so the sidebar sees `IsRunning` change. Make it an `ObservableObject`:

```csharp
    private sealed partial class FakeWork : ObservableObject, IRunningWork
    {
        [ObservableProperty]
        public partial bool IsRunning { get; set; }

        public string? QuitMessage => null;

        public bool Cancelled { get; private set; }

        public Task CancelAndWaitAsync()
        {
            Cancelled = true;
            return Task.CompletedTask;
        }
    }
```

(Add `using CommunityToolkit.Mvvm.ComponentModel;`, `using PS5Kitsune.App.Shell;` and `using PS5Kitsune.Core.Settings;`.)

`GameSettingsViewModelTests`, add a test that `LeaveAsync` asks before discarding unsaved changes and returns false when declined. Follow the file's existing "Back asks before discarding" test:
- change a setting;
- set `_dialogs.Confirm = false`;
- `Assert.False(await page.LeaveAsync())`, with the navigator unchanged and one confirmation.

`GameViewModelTests`: the existing Back tests must still pass unchanged, because Back now goes through `LeaveAsync`.

`HomeViewModelTests`:
- `Create()` becomes `new(_sessions, _settings, _dialogs, _navigator, new FakePages(), new TestPlatform())`.
- Add:

```csharp
    [Fact]
    public async Task Shows_the_platform()
    {
        var viewModel = await CreateAsync();

        Assert.Equal("Test", viewModel.Platform);
        Assert.Equal("test-profile (validated)", viewModel.ProfileText);
    }
```

`LibraryViewModelTests`:
- Rename `Home_and_add_a_game_navigate` to `Add_a_game_navigates`, and drop its `HomeCommand` lines.
- Add:

```csharp
    [Fact]
    public async Task Adding_a_game_needs_an_install()
    {
        _library.Installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.NotInstalled, null);

        var page = await CreateAsync();

        Assert.False(page.CanAddGame);
        Assert.False(page.AddGameCommand.CanExecute(null));
        Assert.Equal("Install AnyPS5 from Home first, then add games.", page.EmptyText);
    }
```

Check `AnyPS5InstallStatus`'s member name for "not installed" in `src/PS5Kitsune.Core/AnyPS5`, and use the one that exists.

`tests/PS5Kitsune.App.Tests/ShellTests.cs` (headless):

```csharp
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Shell;
using PS5Kitsune.App.Tests.Headless;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.App.Views;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

[Collection("Headless")]
public sealed class ShellTests(HeadlessSession session) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public Task The_sidebar_buttons_are_disabled_while_work_runs() => session.RunAsync(() =>
    {
        var navigator = new Navigator();
        var viewModel = new MainWindowViewModel(navigator, new FakeDialogs(), new FakePages(), new ThemeService(new AppSettingsStore(_temp.Combine("s")), _ => { }));
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        navigator.GoTo(new BusyPage());
        Dispatcher.UIThread.RunJobs();

        var library = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Library"));

        Assert.False(library.IsEffectivelyEnabled);
        window.Close();
    });

    private sealed partial class BusyPage : ObservableObject, IRunningWork
    {
        public bool IsRunning => true;

        public string? QuitMessage => null;

        public Task CancelAndWaitAsync() => Task.CompletedTask;
    }

    public void Dispose() => _temp.Dispose();
}
```

`BusyPage` has no view: the `ContentControl` shows it as text, which is fine here.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`. Expected: compile errors.

- [ ] **Step 3: Implement**

`src/PS5Kitsune.App/Navigation/ILeavable.cs`:

```csharp
namespace PS5Kitsune.App.Navigation;

/// <summary>A page with clean-up to run before it is left, by its own Back or by the sidebar.</summary>
public interface ILeavable
{
    /// <summary>Runs the clean-up without navigating.</summary>
    /// <returns>False when the user chose to stay (for example, declined to discard changes).</returns>
    Task<bool> LeaveAsync();
}
```

`Navigator.cs`, add:

```csharp
    /// <summary>The current page, then the pages Back returns to, newest first.</summary>
    public IEnumerable<object> Pages => CurrentPage is null ? _history : _history.Prepend(CurrentPage);
```

`GameViewModel`:
- Implement `ILeavable`: `public sealed partial class GameViewModel : ObservableObject, INavigationAware, ILeavable`.
- Replace `BackToLibraryAsync`:

```csharp
    /// <summary>Stops any launch and stops following the game; the game itself keeps running.</summary>
    public async Task<bool> LeaveAsync()
    {
        if (_launching is { } launching)
        {
            await launching.CancelAsync().ConfigureAwait(true);
        }

        // Leaving must not depend on how the launch ended: an unexpected error is not rethrown here.
        await Launching.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        if (_following is { } following)
        {
            await following.CancelAsync().ConfigureAwait(true);
        }

        await Following.ConfigureAwait(true);
        _playing?.Process.Dispose();
        return true;
    }

    [RelayCommand]
    private async Task BackToLibraryAsync()
    {
        await LeaveAsync().ConfigureAwait(true);
        _navigator.GoHome(_pages.Library());
    }
```

`GameSettingsViewModel`:
- Implement `ILeavable`.
- Replace `BackAsync`:

```csharp
    /// <summary>Asks before discarding unsaved changes, then stops watching the game.</summary>
    public async Task<bool> LeaveAsync()
    {
        if (CanEdit && Current() != _saved
            && !await dialogs.ConfirmAsync("Discard changes?", $"Your changes to the settings of {_name} are not saved.", "Discard").ConfigureAwait(true))
        {
            return false;
        }

        if (_watch is { } watch)
        {
            await watch.CancelAsync().ConfigureAwait(true);
        }

        return true;
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (await LeaveAsync().ConfigureAwait(true))
        {
            navigator.GoBack();
        }
    }
```

`HomeViewModel`:
- Add `IPlatform platform` as the last primary-constructor parameter.
- Add `public string Platform => platform.Name;`.
- Update `PageFactory.Home()` to pass `platform`.

`LibraryViewModel`:
- Delete the `Home` command.
- `Read()` returns the install state with the tiles: change it to `(IReadOnlyList<GameTileViewModel> Tiles, bool Installed)`, and set `Installed = install is not null`.
- In `RefreshAsync`, after `_all = tiles;`, add `CanAddGame = installed;`, with `var (tiles, installed) = await Task.Run(Read)…`.
- Add:

```csharp
    /// <summary>AnyPS5 is installed: games can be added. The sidebar reaches the Library before that.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddGameCommand))]
    [NotifyPropertyChangedFor(nameof(EmptyText))]
    public partial bool CanAddGame { get; private set; }

    public string EmptyText => CanAddGame ? "No games yet. Add one to start." : "Install AnyPS5 from Home first, then add games.";
```

  and `[RelayCommand(CanExecute = nameof(CanAddGame))]` on `AddGame`.

`AppLog.cs`, add:

```csharp
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not leave {Page}.")]
    public static partial void LeaveFailed(this ILogger logger, Exception exception, string page);
```

`MainWindowViewModel.cs`, full replacement:

```csharp
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Shell;
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.App.ViewModels;

public enum NavSection
{
    None,
    Home,
    Library,
}

/// <summary>The window shell: the sidebar (Home, Library, the theme switch) and the page the navigator shows.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IPages _pages;
    private readonly ThemeService _theme;
    private readonly Func<ILogger>? _loggerSource;
    private INotifyPropertyChanged? _watched;

    public MainWindowViewModel(Navigator navigator, IDialogService dialogs, IPages pages, ThemeService theme, Func<ILogger>? loggerSource = null)
    {
        Navigator = navigator;
        _dialogs = dialogs;
        _pages = pages;
        _theme = theme;
        _loggerSource = loggerSource;
        navigator.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Navigator.CurrentPage))
            {
                OnPageChanged();
            }
        };
        OnPageChanged();
    }

    public Navigator Navigator { get; }

    /// <summary>The page runs work the sidebar must not interrupt: an install, an add, a reconvert, reading a game, or a launch's downloads.</summary>
    public bool IsBusy => Navigator.CurrentPage is IRunningWork { IsRunning: true } or GameViewModel { IsPreparing: true };

    public bool CanNavigate => !IsBusy;

    public string? NavTip => IsBusy ? "Finish or cancel the current step first." : null;

    public NavSection CurrentSection => Navigator.CurrentPage switch
    {
        HomeViewModel => NavSection.Home,
        LibraryViewModel => NavSection.Library,
        _ => NavSection.None,
    };

    public bool IsHomeCurrent => CurrentSection == NavSection.Home;

    public bool IsLibraryCurrent => CurrentSection == NavSection.Library;

    public string? HomeItemStatus => IsHomeCurrent ? "current" : null;

    public string? LibraryItemStatus => IsLibraryCurrent ? "current" : null;

    /// <summary>Completes when the last theme choice is saved; for tests.</summary>
    public Task ThemeSaving { get; private set; } = Task.CompletedTask;

    public bool IsSystemTheme
    {
        get => _theme.Current == AppTheme.System;
        set => ChooseTheme(value, AppTheme.System);
    }

    public bool IsLightTheme
    {
        get => _theme.Current == AppTheme.Light;
        set => ChooseTheme(value, AppTheme.Light);
    }

    public bool IsDarkTheme
    {
        get => _theme.Current == AppTheme.Dark;
        set => ChooseTheme(value, AppTheme.Dark);
    }

    /// <summary>The current page runs work or owns a prepared game: closing must go through <see cref="ConfirmCloseAsync"/>.</summary>
    public bool NeedsCloseHandling => Navigator.CurrentPage is IRunningWork { IsRunning: true } or IPreparedGameOwner;

    /// <summary>
    /// Asks before closing when the running work has a quit message; then cancels it and waits, then releases a prepared game.
    /// </summary>
    /// <returns>True when the window may close.</returns>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (Navigator.CurrentPage is IRunningWork { IsRunning: true } work)
        {
            if (work.QuitMessage is { } message && !await _dialogs.ConfirmAsync("Quit PS5Kitsune?", message, "Quit").ConfigureAwait(true))
            {
                return false;
            }

            await work.CancelAndWaitAsync().ConfigureAwait(true);
        }

        if (Navigator.CurrentPage is IPreparedGameOwner owner)
        {
            await owner.ReleaseAsync().ConfigureAwait(true);
        }

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private Task GoHomeAsync() => LeaveAndShowAsync(_pages.Home);

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private Task GoLibraryAsync() => LeaveAndShowAsync(_pages.Library);

    /// <summary>Leaves every page in the stack, current first, as their Back would; a page may keep the user there.</summary>
    private async Task LeaveAndShowAsync(Func<object> target)
    {
        foreach (var page in Navigator.Pages.ToList())
        {
            if (page is not ILeavable leavable)
            {
                continue;
            }

            try
            {
                if (!await leavable.LeaveAsync().ConfigureAwait(true))
                {
                    return;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _loggerSource?.Invoke().LeaveFailed(exception, page.GetType().Name);
            }
        }

        if (Navigator.CurrentPage is IPreparedGameOwner owner)
        {
            try
            {
                await owner.ReleaseAsync().ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _loggerSource?.Invoke().LeaveFailed(exception, owner.GetType().Name);
            }
        }

        Navigator.GoHome(target());
    }

    private void ChooseTheme(bool chosen, AppTheme theme)
    {
        if (!chosen || _theme.Current == theme)
        {
            return;
        }

        // ChangeAsync applies synchronously before its first await, so Current is already the new theme here.
        ThemeSaving = _theme.ChangeAsync(theme);
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    private void OnPageChanged()
    {
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnPagePropertyChanged;
        }

        _watched = Navigator.CurrentPage as INotifyPropertyChanged;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnPagePropertyChanged;
        }

        OnPropertyChanged(nameof(CurrentSection));
        OnPropertyChanged(nameof(IsHomeCurrent));
        OnPropertyChanged(nameof(IsLibraryCurrent));
        OnPropertyChanged(nameof(HomeItemStatus));
        OnPropertyChanged(nameof(LibraryItemStatus));
        OnBusyChanged();
    }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IRunningWork.IsRunning) or nameof(GameViewModel.IsPreparing))
        {
            OnBusyChanged();
        }
    }

    private void OnBusyChanged()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanNavigate));
        OnPropertyChanged(nameof(NavTip));
        GoHomeCommand.NotifyCanExecuteChanged();
        GoLibraryCommand.NotifyCanExecuteChanged();
    }
}
```

`App.axaml.cs`: `window.DataContext = new MainWindowViewModel(navigator, dialogs, pages, theme, () => sessions.Current.Get<ILogger<MainWindowViewModel>>());`. `platform` is still used by `PageFactory`.

`MainWindow.axaml`: keep `Window.DataTemplates` unchanged, and replace the `DockPanel` with:

```xml
  <Grid ColumnDefinitions="168,*">
    <Border Background="{DynamicResource Surface.Sidebar}" BorderBrush="{DynamicResource Border.Subtle}" BorderThickness="0,0,1,0">
      <DockPanel Margin="12,16">
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="4,0,0,20">
          <Image Source="/Assets/kitsune.ico" Width="24" Height="24" AutomationProperties.Name="PS5Kitsune" />
          <TextBlock Text="PS5Kitsune" Classes="h2" VerticalAlignment="Center" />
        </StackPanel>

        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center"
                    c:ArrowSelection.IsEnabled="True">
          <RadioButton GroupName="Theme" Theme="{StaticResource {x:Type ToggleButton}}" Content="◐" Padding="10,4"
                       IsChecked="{Binding IsSystemTheme}" ToolTip.Tip="System theme" AutomationProperties.Name="System theme" />
          <RadioButton GroupName="Theme" Theme="{StaticResource {x:Type ToggleButton}}" Content="☀" Padding="10,4"
                       IsChecked="{Binding IsLightTheme}" ToolTip.Tip="Light theme" AutomationProperties.Name="Light theme" />
          <RadioButton GroupName="Theme" Theme="{StaticResource {x:Type ToggleButton}}" Content="☾" Padding="10,4"
                       IsChecked="{Binding IsDarkTheme}" ToolTip.Tip="Dark theme" AutomationProperties.Name="Dark theme" />
        </StackPanel>

        <StackPanel Spacing="4">
          <Button Classes="nav" Classes.current="{Binding IsHomeCurrent}" Content="Home" Command="{Binding GoHomeCommand}"
                  ToolTip.Tip="{Binding NavTip}" AutomationProperties.ItemStatus="{Binding HomeItemStatus}" />
          <Button Classes="nav" Classes.current="{Binding IsLibraryCurrent}" Content="Library" Command="{Binding GoLibraryCommand}"
                  ToolTip.Tip="{Binding NavTip}" AutomationProperties.ItemStatus="{Binding LibraryItemStatus}" />
        </StackPanel>
      </DockPanel>
    </Border>

    <ContentControl Grid.Column="1" Margin="24" Content="{Binding Navigator.CurrentPage}" />
  </Grid>
```

On the `Window` element, add `xmlns:c="using:PS5Kitsune.App.Controls"`, `Background="{DynamicResource Surface.Page}"`, `FontSize="13"`, and change `MinWidth="600"` to `MinWidth="720"`.

The Library page's own "Home" button is removed in Task 5, together with the rest of its view. Until then, remove only its `Button Grid.Column="3" … HomeCommand` element from `LibraryView.axaml`, so the app builds (`HomeCommand` no longer exists).

`MainWindow.axaml.cs` needs no change.

- [ ] **Step 4: Run the tests**

Run the full App test project: `dotnet test --project tests/PS5Kitsune.App.Tests -c Release`. Expected: PASS, including the existing `GameViewModelTests` Back tests and the `MainWindowViewModelTests` close tests.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Add the sidebar with Home, Library and the theme switch"
```

---

### Task 5: App — the Library grid

**Files:**
- Modify: `src/PS5Kitsune.App/ViewModels/GameTileViewModel.cs`, `src/PS5Kitsune.App/Views/LibraryView.axaml`, `src/PS5Kitsune.App/Views/LibraryView.axaml.cs`
- Test: `tests/PS5Kitsune.App.Tests/LibraryViewModelTests.cs` (tile status), new `tests/PS5Kitsune.App.Tests/LibraryViewTests.cs` (headless)

**Interfaces:**
- Consumes: `StatusKind`, `StatusChip`, `PageView`, `InitialFocus` (Task 3); `LibraryViewModel.CanAddGame` and `EmptyText` (Task 4).
- Produces: `GameTileViewModel.Status` (`StatusKind`), `StatusText` (`string?`), `AutomationName`, `ActionsName`.

- [ ] **Step 1: Write the failing tests**

`LibraryViewModelTests`, add:

```csharp
    [Fact]
    public async Task A_tile_shows_one_status_running_first_then_reconvert_then_compatibility()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00004", "Delta", new ConversionInfo("test-profile", "build", "3fd1cc3", new string('3', 40), false));
        await StartAsync("PPSA00001");

        var page = await CreateAsync();

        Assert.Equal(StatusKind.Running, Tile(page, "Alpha").Status);
        Assert.Equal("Alpha, PPSA00001, Running", Tile(page, "Alpha").AutomationName);
        Assert.Equal(StatusKind.Reconvert, Tile(page, "Delta").Status);
        Assert.Equal("Needs reconvert", Tile(page, "Delta").StatusText);
        Assert.Equal("Actions for Delta", Tile(page, "Delta").ActionsName);
    }
```

(Alpha's verdict in the test library is Orange, as `Lists_the_games_last_played_first_with_their_badges` shows. Running wins over it. Add `using PS5Kitsune.App.Controls;`.)

`tests/PS5Kitsune.App.Tests/LibraryViewTests.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Tests.Headless;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.App.Views;

namespace PS5Kitsune.App.Tests;

[Collection("Headless")]
public sealed class LibraryViewTests(HeadlessSession session) : IDisposable
{
    private readonly TestLibrary _library = new();
    private readonly Navigator _navigator = new();

    [Fact]
    public async Task Arrows_move_between_tiles_and_Enter_opens_the_game()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00002", "Bravo");
        var page = await CreateAsync();

        await session.RunAsync(() =>
        {
            var (window, list) = Show(page);
            list.Focus(NavigationMethod.Tab);
            list.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Assert.Equal(1, list.SelectedIndex);

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.Close();
        });

        // Bravo is not running, so Open launches it: the fake pages return "launch:{titleId}".
        Assert.Equal("launch:PPSA00002", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Shift_F10_opens_the_actions_of_the_selected_tile()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        var opened = await session.RunAsync(() =>
        {
            var (window, list) = Show(page);
            list.Focus(NavigationMethod.Tab);
            list.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            window.KeyPressQwerty(PhysicalKey.F10, RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            var more = list.GetVisualDescendants().OfType<Button>().Single();
            var isOpen = more.Flyout?.IsOpen ?? false;
            more.Flyout?.Hide();
            window.Close();
            return isOpen;
        });

        Assert.True(opened);
    }

    private async Task<LibraryViewModel> CreateAsync()
    {
        var page = new LibraryViewModel(
            _library.Player, _library.Cleaner, _library.Running, _library.Installer, _library.Platform, new FakeDialogs(), _navigator, new FakePages());
        await page.RefreshAsync();
        return page;
    }

    private static (Window Window, ListBox List) Show(LibraryViewModel page)
    {
        var window = new Window { Content = new LibraryView { DataContext = page }, Width = 860, Height = 680 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, window.GetVisualDescendants().OfType<ListBox>().Single());
    }

    public void Dispose() => _library.Dispose();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`. Expected: compile errors (`Status`, `AutomationName`).

- [ ] **Step 3: Implement**

`GameTileViewModel`, add (with `using PS5Kitsune.App.Controls;`):

```csharp
    /// <summary>The one status the tile shows: Running, then Needs reconvert, then the compatibility verdict; Neutral when none.</summary>
    public StatusKind Status => IsRunning ? StatusKind.Running
        : NeedsReconvert ? StatusKind.Reconvert
        : IsRed ? StatusKind.Red
        : IsOrange ? StatusKind.Orange
        : IsGreen ? StatusKind.Green
        : StatusKind.Neutral;

    public string? StatusText => Status switch
    {
        StatusKind.Running => "Running",
        StatusKind.Reconvert => "Needs reconvert",
        StatusKind.Red => "Red",
        StatusKind.Orange => "Orange",
        StatusKind.Green => "Green",
        _ => null,
    };

    /// <summary>What a screen reader says for the tile.</summary>
    public string AutomationName => StatusText is { } status ? $"{Name}, {TitleId}, {status}" : $"{Name}, {TitleId}";

    public string ActionsName => $"Actions for {Name}";
```

`LibraryView.axaml`, full replacement:

```xml
<views:PageView xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:vm="using:PS5Kitsune.App.ViewModels"
                xmlns:views="using:PS5Kitsune.App.Views"
                xmlns:c="using:PS5Kitsune.App.Controls"
                x:Class="PS5Kitsune.App.Views.LibraryView"
                x:DataType="vm:LibraryViewModel">
  <views:PageView.Resources>
    <ControlTheme x:Key="TileItem" TargetType="ListBoxItem">
      <Setter Property="Padding" Value="0" />
      <Setter Property="Margin" Value="0,0,16,16" />
      <Setter Property="Background" Value="Transparent" />
      <Setter Property="Template">
        <ControlTemplate>
          <ContentPresenter Name="PART_ContentPresenter" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}"
                            Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" />
        </ControlTemplate>
      </Setter>
    </ControlTheme>
  </views:PageView.Resources>
  <views:PageView.Styles>
    <Style Selector="ListBox.tiles > ListBoxItem" x:DataType="vm:GameTileViewModel">
      <Setter Property="AutomationProperties.Name" Value="{Binding AutomationName}" />
    </Style>
    <Style Selector="Border.icon">
      <Setter Property="BorderThickness" Value="2" />
      <Setter Property="BorderBrush" Value="Transparent" />
    </Style>
    <Style Selector="ListBoxItem:selected Border.icon">
      <Setter Property="BorderBrush" Value="{DynamicResource Accent}" />
    </Style>
    <Style Selector="Button.more">
      <Setter Property="Opacity" Value="0" />
    </Style>
    <Style Selector="ListBoxItem:pointerover Button.more, ListBoxItem:selected Button.more, ListBoxItem:focus-within Button.more">
      <Setter Property="Opacity" Value="1" />
    </Style>
  </views:PageView.Styles>

  <DockPanel>
    <Grid DockPanel.Dock="Top" ColumnDefinitions="Auto,*,Auto" ColumnSpacing="12" Margin="0,0,0,16">
      <TextBlock Text="Library" Classes="h1" VerticalAlignment="Center" />
      <TextBox Grid.Column="1" Text="{Binding Search}" PlaceholderText="Search by name" MaxWidth="320" HorizontalAlignment="Left"
               IsVisible="{Binding !IsEmpty}" AutomationProperties.Name="Search by name" c:InitialFocus.Rank="1" />
      <Button Grid.Column="2" Content="Add a game" Classes="accent" Command="{Binding AddGameCommand}" />
    </Grid>

    <TextBlock DockPanel.Dock="Top" Text="{Binding Message}" Classes="error" Margin="0,0,0,8"
               IsVisible="{Binding Message, Converter={x:Static ObjectConverters.IsNotNull}}"
               AutomationProperties.LiveSetting="Assertive" />

    <StackPanel DockPanel.Dock="Top" Spacing="8" IsVisible="{Binding IsEmpty}" HorizontalAlignment="Left">
      <TextBlock Text="{Binding EmptyText}" Classes="caption" />
      <Button Content="Add a game" Classes="accent" Command="{Binding AddGameCommand}" c:InitialFocus.Rank="2" />
    </StackPanel>

    <ListBox x:Name="Games" Classes="tiles" ItemsSource="{Binding Games}" SelectionMode="Single" Background="Transparent"
             ItemContainerTheme="{StaticResource TileItem}" AutomationProperties.Name="Games"
             KeyboardNavigation.TabNavigation="Once" ScrollViewer.HorizontalScrollBarVisibility="Disabled">
      <ListBox.ItemsPanel>
        <ItemsPanelTemplate>
          <WrapPanel />
        </ItemsPanelTemplate>
      </ListBox.ItemsPanel>
      <ListBox.ItemTemplate>
        <DataTemplate x:DataType="vm:GameTileViewModel">
          <StackPanel Width="136" Spacing="2">
            <Panel Width="136" Height="136">
              <Border Classes="icon" CornerRadius="8" ClipToBounds="True" Background="{DynamicResource Surface.Raised}">
                <Panel>
                  <TextBlock Text="{Binding TitleId}" Classes="caption" HorizontalAlignment="Center" VerticalAlignment="Center"
                             IsVisible="{Binding IconPath, Converter={x:Static ObjectConverters.IsNull}}" />
                  <Image Source="{Binding IconPath, Converter={x:Static views:PathToBitmapConverter.Instance}}" Stretch="UniformToFill"
                         AutomationProperties.Name="{Binding Name}" />
                </Panel>
              </Border>
              <c:StatusChip Kind="{Binding Status}" Text="{Binding StatusText}" Margin="6"
                            HorizontalAlignment="Left" VerticalAlignment="Bottom"
                            IsVisible="{Binding StatusText, Converter={x:Static ObjectConverters.IsNotNull}}" />
              <Button Classes="more" Content="…" Padding="8,0" Margin="6" Focusable="False"
                      HorizontalAlignment="Right" VerticalAlignment="Top"
                      Background="{DynamicResource Surface.Raised}" AutomationProperties.Name="{Binding ActionsName}">
                <Button.Flyout>
                  <MenuFlyout>
                    <MenuItem Header="Launch" Command="{Binding Owner.LaunchCommand}" CommandParameter="{Binding}" />
                    <MenuItem Header="Settings" Command="{Binding Owner.SettingsCommand}" CommandParameter="{Binding}" />
                    <MenuItem Header="Open folder" Command="{Binding Owner.OpenFolderCommand}" CommandParameter="{Binding}" />
                    <MenuItem Header="View last log" Command="{Binding Owner.ViewLastLogCommand}" CommandParameter="{Binding}" />
                    <MenuItem Header="Reconvert" Command="{Binding Owner.ReconvertCommand}" CommandParameter="{Binding}" />
                    <MenuItem Header="Clear shader cache" Command="{Binding Owner.ClearShaderCacheCommand}" CommandParameter="{Binding}" />
                    <MenuItem Header="Remove" Command="{Binding Owner.RemoveCommand}" CommandParameter="{Binding}" />
                  </MenuFlyout>
                </Button.Flyout>
              </Button>
            </Panel>
            <TextBlock Text="{Binding Name}" FontWeight="SemiBold" TextTrimming="CharacterEllipsis" Margin="0,6,0,0" />
            <TextBlock Text="{Binding TitleId}" Classes="caption" />
          </StackPanel>
        </DataTemplate>
      </ListBox.ItemTemplate>
    </ListBox>
  </DockPanel>
</views:PageView>
```

`LibraryView.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class LibraryView : PageView
{
    public LibraryView()
    {
        InitializeComponent();
        Games.AddHandler(KeyDownEvent, OnGamesKeyDown, RoutingStrategies.Tunnel);
        Games.AddHandler(Gestures.TappedEvent, OnGamesTapped);
    }

    /// <summary>Enter opens the game; the Menu key or Shift+F10 opens its actions, as "…" does with the mouse.</summary>
    private void OnGamesKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not LibraryViewModel page || CurrentTile() is not { } tile)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            page.OpenCommand.Execute(tile);
            e.Handled = true;
        }
        else if (e.Key == Key.Apps || (e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift))
        {
            OpenActions(tile);
            e.Handled = true;
        }
    }

    /// <summary>One click opens the game, as before the grid became a list; a click on "…" only opens its menu.</summary>
    private void OnGamesTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is LibraryViewModel page && e.Source is Visual source
            && source.FindAncestorOfType<Button>(includeSelf: true) is null
            && source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { DataContext: GameTileViewModel tile })
        {
            page.OpenCommand.Execute(tile);
        }
    }

    /// <summary>The focused tile, else the selected one.</summary>
    private GameTileViewModel? CurrentTile() =>
        (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as GameTileViewModel
        ?? Games.SelectedItem as GameTileViewModel;

    private void OpenActions(GameTileViewModel tile)
    {
        if (Games.ContainerFromItem(tile) is Control container && container.FindDescendantOfType<Button>() is { Flyout: { } flyout } more)
        {
            flyout.ShowAt(more);
        }
    }
}
```

`PathToBitmapConverter` stays in `Views/`, unchanged. `OpenCommand` is the generated `IAsyncRelayCommand<GameTileViewModel>` of `OpenAsync`.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "PS5Kitsune.App.Tests.LibraryViewTests"`, then `PS5Kitsune.App.Tests.LibraryViewModelTests`. Expected: PASS.

If `ListBox` handles Enter before the tunnel handler (it should not, since the tunnel runs first), report it.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Show the library as an icon grid driven by the keyboard"
```

---

### Task 6: App — the Game page and its download card, and the Settings page

**Files:**
- Modify: `src/PS5Kitsune.App/ViewModels/GameViewModel.cs`, `src/PS5Kitsune.App/Views/GameView.axaml`, `src/PS5Kitsune.App/Views/GameView.axaml.cs`, `src/PS5Kitsune.App/Views/GameSettingsView.axaml`, `src/PS5Kitsune.App/Views/GameSettingsView.axaml.cs`
- Test: `tests/PS5Kitsune.App.Tests/GameViewModelTests.cs`

**Interfaces:**
- Consumes: `PageView`, `InitialFocus` (Task 3); `GameViewModel.LeaveAsync` (Task 4).
- Produces: `GameViewModel.DownloadValue` (`double`, 0–1), `IsDownloadIndeterminate` (`bool`), `HasLog` (`bool`).

- [ ] **Step 1: Write the failing tests**

In `GameViewModelTests`, find the existing test that reports a Lavapipe download (search for "Downloading Lavapipe"). Next to it, add a test with the same setup that also asserts:
- after a report with a known total of 100 MB and 25 MB received: `DownloadValue` is 0.25 and `IsDownloadIndeterminate` is false;
- after a report with an unknown total: `IsDownloadIndeterminate` is true;
- after the `Waiting` report: `IsDownloadIndeterminate` is true and `DownloadValue` is 0;
- once the game runs: `IsDownloadIndeterminate` is true and `DownloadValue` is 0.

Also add `HasLog_follows_the_log_lines`, built on the file's existing pattern for a line that reaches the page:
- Find the test that asserts `page.Log` contains a line, and copy its arrange-and-wait part.
- Subscribe to `PropertyChanged` before the line arrives.
- Assert that `HasLog` is false before and true after, and that `nameof(GameViewModel.HasLog)` was raised.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`. Expected: compile errors.

- [ ] **Step 3: Implement the view model**

`GameViewModel`:

```csharp
    /// <summary>The download's progress from 0 to 1; 0 when unknown.</summary>
    [ObservableProperty]
    public partial double DownloadValue { get; private set; }

    /// <summary>True unless a download with a known size is running.</summary>
    [ObservableProperty]
    public partial bool IsDownloadIndeterminate { get; private set; } = true;

    /// <summary>The log has at least one line; until then the page says where the game's output will appear.</summary>
    public bool HasLog => Log.Count > 0;
```

- At the end of the second constructor (the `GameLayout` one), add `Log.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasLog));`. The first constructor chains to it.
- In `ShowDownload`:
  - set `DownloadValue = 0; IsDownloadIndeterminate = true;` in the `Waiting` branch;
  - in the size branch, set `IsDownloadIndeterminate = value.Progress.TotalBytes is not > 0;` and `DownloadValue = value.Progress.TotalBytes is > 0 and var total ? (double)value.Progress.ReceivedBytes / total : 0;`.
- Wherever `IsPreparing = false;` is set inside `LaunchAsync` (the `finally`, around line 188), also reset `DownloadValue = 0; IsDownloadIndeterminate = true;`.

- [ ] **Step 4: Rewrite the views**

`GameView.axaml`:

```xml
<views:PageView xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:vm="using:PS5Kitsune.App.ViewModels"
                xmlns:views="using:PS5Kitsune.App.Views"
                xmlns:c="using:PS5Kitsune.App.Controls"
                xmlns:sys="using:System"
                x:Class="PS5Kitsune.App.Views.GameView"
                x:DataType="vm:GameViewModel">
  <DockPanel>
    <StackPanel DockPanel.Dock="Top" Spacing="6">
      <TextBlock Text="{Binding Title}" Classes="h1" />
      <TextBlock Text="{Binding StatusText}" IsVisible="{Binding !IsPreparing}" AutomationProperties.LiveSetting="Polite" />
      <TextBlock Text="{Binding FirstLaunchHint}" Classes="caption"
                 IsVisible="{Binding FirstLaunchHint, Converter={x:Static ObjectConverters.IsNotNull}}" />
      <TextBlock Text="{Binding FontsWarning}" Classes="warning"
                 IsVisible="{Binding FontsWarning, Converter={x:Static ObjectConverters.IsNotNull}}" />
      <TextBlock Text="{Binding Message}" Classes="error" AutomationProperties.LiveSetting="Assertive"
                 IsVisible="{Binding Message, Converter={x:Static ObjectConverters.IsNotNull}}" />
      <Border Classes="card error" Margin="0,6,0,0" AutomationProperties.LiveSetting="Assertive"
              IsVisible="{Binding Diagnosis, Converter={x:Static ObjectConverters.IsNotNull}}">
        <StackPanel DataContext="{Binding Diagnosis}" x:DataType="vm:DiagnosisViewModel" Spacing="8">
          <TextBlock Text="{Binding Title}" Classes="h2" />
          <TextBlock Text="{Binding Explanation}" TextWrapping="Wrap" />
          <SelectableTextBlock Text="{Binding LineText}" Classes="mono"
                               IsVisible="{Binding LineText, Converter={x:Static ObjectConverters.IsNotNull}}" />
          <StackPanel Orientation="Horizontal" Spacing="12">
            <Button Content="{Binding ActionText}" Classes="accent" Command="{Binding FixCommand}" IsVisible="{Binding HasAction}" />
            <TextBlock Text="{Binding Result}" Classes="caption" VerticalAlignment="Center"
                       IsVisible="{Binding Result, Converter={x:Static ObjectConverters.IsNotNull}}" />
          </StackPanel>
        </StackPanel>
      </Border>
    </StackPanel>

    <Border DockPanel.Dock="Bottom" Classes="footer">
      <DockPanel>
        <StackPanel Orientation="Horizontal" Spacing="12">
          <Button Content="Open folder" Command="{Binding OpenFolderCommand}" />
          <Button Content="Settings" Command="{Binding OpenSettingsCommand}" />
        </StackPanel>
        <StackPanel Orientation="Horizontal" Spacing="12" HorizontalAlignment="Right">
          <Button Content="← Library" Command="{Binding BackToLibraryCommand}" IsCancel="True" c:InitialFocus.Rank="3" />
          <Button Content="Cancel" Command="{Binding CancelLaunchCommand}" IsVisible="{Binding IsPreparing}" />
          <Button Content="Stop" Command="{Binding StopCommand}" IsVisible="{Binding IsRunning}" c:InitialFocus.Rank="2" />
          <Button Content="Launch again" Classes="accent" Command="{Binding LaunchAgainCommand}" IsDefault="True"
                  IsVisible="{Binding CanLaunchAgain}" c:InitialFocus.Rank="1" />
        </StackPanel>
      </DockPanel>
    </Border>

    <Panel Margin="0,16,0,0">
      <Border Classes="card" VerticalAlignment="Top" IsVisible="{Binding IsPreparing}">
        <StackPanel Spacing="8">
          <TextBlock Text="{Binding StatusText}" AutomationProperties.LiveSetting="Polite" />
          <ProgressBar Minimum="0" Maximum="1" Value="{Binding DownloadValue}" IsIndeterminate="{Binding IsDownloadIndeterminate}"
                       AutomationProperties.Name="Download progress" />
        </StackPanel>
      </Border>
      <Border Classes="log" IsVisible="{Binding !IsPreparing}">
        <Panel>
          <TextBlock Text="The game's output appears here." Classes="caption" HorizontalAlignment="Center" VerticalAlignment="Center"
                     IsVisible="{Binding !HasLog}" />
          <ScrollViewer x:Name="LogScroller" IsVisible="{Binding HasLog}">
            <ItemsControl ItemsSource="{Binding Log}">
              <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="sys:String">
                  <TextBlock Text="{Binding}" Classes="mono" />
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>
          </ScrollViewer>
        </Panel>
      </Border>
    </Panel>
  </DockPanel>
</views:PageView>
```

`GameView.axaml.cs`: change the base from `UserControl` to `PageView`, and leave the rest unchanged.

`GameSettingsView.axaml`. Same bindings as today, restyled:
- **Root:** `views:PageView` (with `xmlns:views` and `xmlns:c`). The code-behind base becomes `PageView`.
- **Top `StackPanel`:**
  - title `Classes="h1"`;
  - `RunningNotice` in a `Border Classes="card"`, with `AutomationProperties.LiveSetting="Polite"` on its `TextBlock`;
  - `Message` with `Classes="error"` and `LiveSetting="Assertive"`;
  - `Notice` as plain body text with `LiveSetting="Polite"`.
  - Remove every `Foreground="#…"`.
- **Footer:** `<Border DockPanel.Dock="Bottom" Classes="footer">`. On the left, `Reset to defaults`. On the right, `Back` (`IsCancel="True"`, `c:InitialFocus.Rank="3"`) and `Save` (`Classes="accent"`, `IsDefault="True"`). Spacing is 12, with the same `DockPanel` shape as the Game page.
- **Content:** each setting `StackPanel` (`Spacing="4"`) keeps its controls. Each label `TextBlock` gets `x:Name` (`GpuLabel`, `LanguageLabel`, `VsyncLabel`, `OverlayPositionLabel`, `PlayGoLabel`) and `FontWeight="SemiBold"`.
  - Each `ComboBox` gets `AutomationProperties.LabeledBy="{Binding #GpuLabel}"` (and so on).
  - Help texts get `Classes="caption"` instead of `Opacity`.
  - The GPU `ComboBox` gets `c:InitialFocus.Rank="1"`, and the Lavapipe `CheckBox` gets `c:InitialFocus.Rank="2"`.
  - The setting stack's `Spacing` becomes 16.

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "PS5Kitsune.App.Tests.GameViewModelTests"`, then `GameSettingsViewModelTests`. Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Show a download card on the Game page and restyle it and Settings"
```

---

### Task 7: App — every other page, and the no-literal-colors rule

**Files:**
- Modify the views, and the base class in every code-behind (`UserControl` → `PageView`): `HomeView`, `SystemCheckView`, `ModeView`, `ToolsView`, `InstallView`, `DoneView`, `ChooseGameView`, `GameSummaryView`, `AddGameView`, `AddResultView`, `ReconvertView`.
- Modify: `src/PS5Kitsune.App/ViewModels/ModeViewModel.cs` (settable selection), `ToolRowViewModel.cs` (`Kind`), `AddResultViewModel.cs` (`VerdictKind`, `VerdictLabel`)
- Delete: `src/PS5Kitsune.App/Converters/CheckStatusBrushConverter.cs`
- Test: `tests/PS5Kitsune.App.Tests/ThemeTests.cs` (no color literals), `ModeViewModelTests.cs`, `ToolsViewModelTests.cs`, `AddResultViewModelTests.cs`, and `ControlsTests.cs` (Mode's cards with the arrow keys)

**Interfaces:**
- Consumes: Tasks 2 and 3 (tokens, classes, `OptionCard`, `StatusChip`, `ArrowSelection`, `InitialFocus`, `PageView`, `CheckStatusKindConverter`).
- Produces:
  - `ModeViewModel.IsReleaseSelected` and `IsBuildSelected`, now settable;
  - `ToolRowViewModel.Kind`;
  - `AddResultViewModel.VerdictKind` and `VerdictLabel`.

- [ ] **Step 1: Write the failing tests**

`ThemeTests`, add:

```csharp
    [Fact]
    public void Views_hold_no_color_literals()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "PS5Kitsune.App", "Views");
        var literal = new System.Text.RegularExpressions.Regex("\"#[0-9A-Fa-f]{3,8}\"|\"(White|Black)\"");

        var offenders = Directory.EnumerateFiles(views, "*.axaml")
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, index)))
            .Where(entry => literal.IsMatch(entry.line))
            .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.index + 1}: {entry.line.Trim()}")
            .ToList();

        Assert.Empty(offenders);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PS5Kitsune.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("PS5Kitsune.slnx not found above the test folder.");
    }
```

This test does not use the headless session, but it can live in the `[Collection("Headless")]` class.

`ModeViewModelTests`, add:

```csharp
    [Fact]
    public void Checking_a_card_selects_its_mode()
    {
        var viewModel = Create();   // the file's existing helper with a build-capable installer

        viewModel.IsBuildSelected = true;
        Assert.Equal(AnyPS5Mode.Build, viewModel.SelectedMode);

        viewModel.IsReleaseSelected = true;
        Assert.Equal(AnyPS5Mode.Release, viewModel.SelectedMode);

        viewModel.IsReleaseSelected = false;   // unchecking never selects nothing
        Assert.Equal(AnyPS5Mode.Release, viewModel.SelectedMode);
    }
```

Use the file's existing construction helper. If a test needs an installer that supports Build, copy the one used by its Build tests.

`ToolsViewModelTests`: assert that an installed tool's `Kind` is `StatusKind.Green` and a missing one's is `StatusKind.Neutral`.

`AddResultViewModelTests`:
- a Green audit gives `VerdictKind == StatusKind.Green` and `VerdictLabel == "Green"`;
- an Orange audit gives `Orange` and `"Orange"`.

Use the file's existing audit builders.

`ControlsTests`, add a headless test:
- Show a `ModeView` whose `DataContext` is a `ModeViewModel` able to build, built as in `ModeViewModelTests`.
- Focus the release `RadioButton` (`NavigationMethod.Tab`) and press ArrowRight.
- Assert that `SelectedMode` is `Build` and that the build card `IsChecked`.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`. Expected: compile errors (the setters, `Kind`, `VerdictKind`). Once those are added, `Views_hold_no_color_literals` fails and lists today's literals.

- [ ] **Step 3: Implement the view model changes**

`ModeViewModel`:

```csharp
    /// <summary>Bound two-way to the release card: checking it selects Release; unchecking is ignored (the other card's check selects).</summary>
    public bool IsReleaseSelected
    {
        get => SelectedMode == AnyPS5Mode.Release;
        set
        {
            if (value)
            {
                SelectedMode = AnyPS5Mode.Release;
            }
        }
    }

    public bool IsBuildSelected
    {
        get => SelectedMode == AnyPS5Mode.Build;
        set
        {
            if (value && CanBuild)
            {
                SelectedMode = AnyPS5Mode.Build;
            }
        }
    }
```

These replace the two expression-bodied properties. The `NotifyPropertyChangedFor` attributes on `SelectedMode` stay.

`ToolRowViewModel`: `public StatusKind Kind => info.IsInstalled ? StatusKind.Green : StatusKind.Neutral;`

`AddResultViewModel`:

```csharp
    public StatusKind VerdictKind => Verdict switch
    {
        AuditVerdict.Green => StatusKind.Green,
        AuditVerdict.Orange => StatusKind.Orange,
        _ => StatusKind.Red,
    };

    public string VerdictLabel => Verdict switch
    {
        AuditVerdict.Green => "Green",
        AuditVerdict.Orange => "Orange",
        _ => "Red",
    };
```

(Check the `AuditVerdict` members. If a fourth value exists, map it to `StatusKind.Neutral` with its own name.)

- [ ] **Step 4: Rewrite the views**

For each view: the root becomes `views:PageView` (add `xmlns:views="using:PS5Kitsune.App.Views"` and `xmlns:c="using:PS5Kitsune.App.Controls"`), and the code-behind's base becomes `PageView`. Bindings and commands stay as they are, except where noted. Apply these rules everywhere:

- **Title:** `TextBlock` with `Classes="h1"` (no `FontSize`/`FontWeight`).
- **Secondary text:**
  - `Opacity="0.85"` or `0.8` becomes plain body text, or `Classes="caption"` for a hint or small note.
  - `Opacity="0.7" FontSize="12"` becomes `Classes="caption"`.
- **Messages:**
  - `Foreground="#C42B1C"` becomes `Classes="error"` with `AutomationProperties.LiveSetting="Assertive"`.
  - `Foreground="#9D5D00"` becomes `Classes="warning"`.
- **Mono:** the `FontFamily="Cascadia Mono,…" FontSize="12"` pair becomes `Classes="mono"`.
- **Logs:** `<Border … Background="#14808080">` becomes `<Border Classes="log" Margin="0,16,0,0">`.
- **Footer:** the bottom button `StackPanel` is wrapped in `<Border DockPanel.Dock="Bottom" Classes="footer">`, with `Spacing="12"`.
  - Secondary actions (Open log, Open folder…) go left; Back or Cancel and the primary button go right.
  - The primary button has `Classes="accent"`, `IsDefault="True"` and `c:InitialFocus.Rank="1"`.
  - Back has `IsCancel="True"`.
- **Progress bars:** each gets an `AutomationProperties.Name`: "Checking" on Home and System check, and "Install progress", "Add progress" and "Reconvert progress" on the three step pages.
- **Status lines:** progress text lines on Install, Add game and Reconvert get `AutomationProperties.LiveSetting="Polite"`.

Per view:

- **HomeView:**
  - The state card becomes `<Border Classes="card">`. Its title gets `Classes="h2"`, and the progress bar gets `AutomationProperties.Name="Checking"`.
  - The three buttons go in one horizontal row (`Spacing="12"`, wrapping in a `WrapPanel` if needed): the primary (accent, `c:InitialFocus.Rank="1"`), "Add a game" and "Library". Below them, `AddGameHint` as `caption`.
  - The "Data folder" and "AnyPS5 profile" sections become one **This PC** section: an `h2` "This PC", then a `Grid ColumnDefinitions="Auto,*,Auto" RowDefinitions="Auto,Auto,Auto" ColumnSpacing="12" RowSpacing="8"` with:
    - "Platform" / `{Binding Platform}`;
    - "Data folder" / `{Binding DataRoot}` (ellipsis) / the `Change…` button;
    - "AnyPS5 profile" / `{Binding ProfileText}`.

    The labels are `caption`. Under the grid come `RootError` (`error`) and the ASCII rule line (`caption`).
- **SystemCheckView:**
  - The banner becomes `<Border DockPanel.Dock="Top" Classes="card">` holding a `Grid ColumnDefinitions="Auto,*,Auto"`:
    - a `c:StatusChip Kind="{Binding BannerStatus, Converter={x:Static conv:CheckStatusKindConverter.Instance}}" Text="{Binding BannerLabel}"`;
    - the progress bar and `Verdict` (`Classes="h2"`);
    - "Check again".
  - Add `BannerLabel` to `SystemCheckViewModel`: "Checking" while `BannerStatus` is null, else the same word `CheckRowViewModel.StatusText` uses for that status. Read `CheckRowViewModel.StatusText`'s mapping and reuse its words through a shared static method `CheckRowViewModel.Label(CheckStatus)`. Add a test in `SystemCheckViewModelTests` that `BannerLabel` follows `BannerStatus`.
  - The check rows' badge `Border` becomes `c:StatusChip Kind="{Binding Status, Converter={x:Static conv:CheckStatusKindConverter.Instance}}" Text="{Binding StatusText}"`.
  - `Summary` becomes body text, `Action` becomes `caption`, and `FixError` becomes `error`.
  - The rows sit inside one `Border Classes="card"`, with a `Border Classes="divider"` between rows. Put the divider at the top of each row's template, with `Margin="0,0,0,10"`, and add `IsVisible` only where the first row can be told apart. If not, keep plain 12 px spacing and no dividers.
- **ModeView:**
  - Remove the local `Button.card` styles.
  - The `Grid` of two cards gets `c:ArrowSelection.IsEnabled="True"`. Each card becomes `<RadioButton GroupName="Mode" Theme="{StaticResource OptionCard}" IsChecked="{Binding IsReleaseSelected}">` (and `IsBuildSelected`, with `IsEnabled="{Binding CanBuild}"`), keeping the same `StackPanel` content without the two "Selected" lines.
  - The card titles get `Classes="h2"`.
  - `ErrorText` becomes `error`. Continue is the primary button.
- **ToolsView:**
  - Remove the local `Border` styles.
  - The `ItemsControl` sits in `<Border Classes="card">`. Each row's badge becomes `c:StatusChip Kind="{Binding Kind}" Text="{Binding StatusText}"`, the version becomes `caption`, and rows are separated by 8 px.
- **InstallView, AddGameView, ReconvertView** (the same shape):
  - The step list sits in a `Border Classes="card"`, with `StateText` as `caption`.
  - Footer: "Open log" on the left. On the right, Cancel (`c:InitialFocus.Rank="2"`, `IsCancel="True"`), "Back to home" or "Go to library", and Retry (accent, `IsDefault="True"`, `c:InitialFocus.Rank="1"`).
- **DoneView:** "Back to home" is the primary button. The two path rows use `caption` labels.
- **ChooseGameView:**
  - The drop zone `Border` gets `BorderBrush="{DynamicResource Border.Strong}"` and `CornerRadius="8"`.
  - "Choose a folder…" gets `c:InitialFocus.Rank="1"`.
  - The footer holds Back alone (`IsCancel="True"`).
  - The progress bar gets the automation name "Reading the game".
- **GameSummaryView:**
  - The icon placeholder `Border Background="#20808080"` becomes `Background="{DynamicResource Surface.Raised}"` with `CornerRadius="8"`.
  - The name gets `Classes="h1"`, and the title ID `caption`.
  - Blocking issues become `error` and warnings `warning`.
  - The "already added" block becomes a `Border Classes="card"`.
  - "Add the game" is the primary button.
- **AddResultView:**
  - The three colored verdict lines become one row: `c:StatusChip Kind="{Binding VerdictKind}" Text="{Binding VerdictLabel}"`, then `TextBlock Text="{Binding VerdictText}" FontWeight="SemiBold"`.
  - `MissingLibrariesText` becomes `error`, and warnings `warning`.
  - "Go to library" is the primary button.

Delete `Converters/CheckStatusBrushConverter.cs` once no view references it.

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/PS5Kitsune.App.Tests -c Release`. Expected: PASS, including `Views_hold_no_color_literals`.

Then build the whole solution. Expected: 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add -A src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Restyle every page with the theme's tokens, cards and chips"
```

---

### Task 8: App — the page gallery: first focus on every page, and screenshots

**Files:**
- Create: `tests/PS5Kitsune.App.Tests/Headless/PageGallery.cs`, `tests/PS5Kitsune.App.Tests/FocusTests.cs`, `tests/PS5Kitsune.App.Tests/Screenshots.cs`

**Interfaces:**
- Consumes: every page view model and view; the fakes in `Fakes.cs` (`TestLibrary`, `FakePages`, `FakeDialogs`, `FakeInstaller`, `TestSessions`, `TestGameData`, `TestPlatform`, `InlineDispatcher`); `MainWindow` and `MainWindowViewModel` (Task 4).
- Produces: `PageGallery`, which builds each page with fake data and is used by both test classes.

- [ ] **Step 1: Write the gallery**

`PageGallery` is a test helper (`IDisposable`; it owns a `TestLibrary` and a `TempDirectory`).
- It exposes `Task<IReadOnlyList<(string Name, object Page)>> BuildAsync()`.
- It returns the pages below, built as the existing view model tests build them. **Copy each construction from the named test file**: these are the same constructors with the same fakes.

  | Name | Built like | Data |
  |---|---|---|
  | `Home` | `HomeViewModelTests.Create()` + `RefreshAsync()` | installed release |
  | `SystemCheck` | `SystemCheckViewModelTests` | one Ok, one Warning, one Blocking check; checked |
  | `Mode` | `ModeViewModelTests` | build available, Release selected |
  | `Tools` | `ToolsViewModelTests` | one tool installed, two missing |
  | `Install-Running` | `InstallViewModelTests` | install blocked on `Task.Delay(Timeout.Infinite, token)`, three steps, some log lines |
  | `Install-Failed` | `InstallViewModelTests` | install throws `IOException("disk gone")` |
  | `Done` | `DoneViewModelTests` | — |
  | `ChooseGame` | `ChooseGameViewModelTests` | — |
  | `GameSummary` | `GameSummaryViewModelTests` | one warning |
  | `AddGame` | `AddGameViewModelTests` | running, as Install-Running |
  | `AddResult` | `AddResultViewModelTests` | Orange audit with one warning |
  | `Reconvert` | `ReconvertViewModelTests` | running |
  | `Library` | `LibraryViewModelTests.CreateAsync()` | four games: Running, Needs reconvert, Green, Red; two with an icon (write a 64×64 PNG with `Avalonia.Media.Imaging.RenderTargetBitmap` into the game's folder and set the record's icon as `AddAsync` allows; if `AddAsync` has no icon parameter, skip icons and say so in the report) |
  | `Library-Empty` | same, with no games | — |
  | `Game-Preparing` | `GameViewModelTests` | Lavapipe on, the fake download blocked mid-way with a known total |
  | `Game-Running-NoLog` | `GameViewModelTests` | game started, no log line |
  | `Game-Running-Log` | `GameViewModelTests` | game started, twenty log lines |
  | `Game-Diagnosis` | `GameViewModelTests`, the diagnosis tests (`LaunchAsync(diagnose: true)` with the GPU error line) | the game exited with the GPU error |
  | `GameSettings` | `GameSettingsViewModelTests` | game not running |

  Pages that need `RefreshAsync` or a launch are awaited to the state named, through the same waits the existing tests use. Running work is cancelled in `Dispose`.

- [ ] **Step 2: Write the focus tests**

`FocusTests` (`[Collection("Headless")]`), one theory over the gallery:
- For each page, show a `MainWindow` with `DataContext = new MainWindowViewModel(navigator, …)`, then `navigator.GoTo(page)`.
- Run `Dispatcher.UIThread.RunJobs()` twice, so `PageView`'s posted focus runs.
- Read `window.FocusManager!.GetFocusedElement()`.

Expected focus, by the control's text (`Button.Content` as a string, `TextBox` by its `AutomationProperties.Name`, `ComboBox` by its label's text through `LabeledBy`):

| Page | Focused |
|---|---|
| Home | the primary action button (`ActionText`) |
| SystemCheck | "Continue" |
| Mode, Tools | the Continue button (`ContinueText`) |
| Install-Running, AddGame, Reconvert | "Cancel" |
| Install-Failed | "Retry" |
| Done | "Back to home" |
| ChooseGame | "Choose a folder…" |
| GameSummary | "Add the game" |
| AddResult | "Go to library" |
| Library | the search box ("Search by name") |
| Library-Empty | "Add a game" (the empty-state button) |
| Game-Preparing | "← Library" |
| Game-Running-NoLog, Game-Running-Log | "Stop" |
| Game-Diagnosis | "Launch again" |
| GameSettings | the GPU combo box |

Write the expectations as a `TheoryData<string, string>` of (page name, expected text). The test fails with the actual focused element's type and text.

- [ ] **Step 3: Write the screenshots test**

`Screenshots` (`[Collection("Headless")]`):

```csharp
    [Fact]
    public async Task Render_every_page()
    {
        var folder = Environment.GetEnvironmentVariable("PS5K_SCREENSHOTS");
        if (string.IsNullOrEmpty(folder))
        {
            Assert.Skip("Set PS5K_SCREENSHOTS to a folder to render every page.");
        }

        Directory.CreateDirectory(folder);
        using var gallery = new PageGallery();
        foreach (var (name, page) in await gallery.BuildAsync())
        {
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                await session.RunAsync(() =>
                {
                    Application.Current!.RequestedThemeVariant = variant;
                    var navigator = new Navigator();
                    var window = new MainWindow
                    {
                        DataContext = new MainWindowViewModel(navigator, new FakeDialogs(), new FakePages(), new ThemeService(new AppSettingsStore(gallery.SettingsFolder), _ => { })),
                        Width = 860,
                        Height = 680,
                    };
                    window.Show();
                    navigator.GoTo(page);
                    Dispatcher.UIThread.RunJobs();
                    window.CaptureRenderedFrame()!.Save(Path.Combine(folder, $"{name}-{variant.Key}.png"));
                    window.Close();
                });
            }
        }
    }
```

`PageGallery.SettingsFolder` is a folder in its temp directory. Close the window's flyouts and reset the variant to `Default` at the end.

- [ ] **Step 4: Run them**

Run:
- `dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "PS5Kitsune.App.Tests.FocusTests"`. Expected: PASS. A failure means a view's `InitialFocus.Rank` is wrong: fix the view, not the expectation, unless the expectation contradicts the spec's "First focus" list.
- Then render the screenshots into the scratch folder `C:\PS5KScreens` (not the repo):

```bash
PS5K_SCREENSHOTS='C:\PS5KScreens' dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "PS5Kitsune.App.Tests.Screenshots"
```

Expected: 38 PNG files (19 pages × 2 variants). Do not add them to git.

- [ ] **Step 5: Commit**

```bash
git add tests/PS5Kitsune.App.Tests
git commit -m "Check every page's first focus, and render every page in both themes"
```

**Controller review after Task 8:** the controller reads the PNGs (Light and Dark of each page). It checks that each screen matches the spec's design: tokens, cards, footer, type, no Fluent-blue leftovers, readable chips, the sidebar's current item, and the Game page's three log states. Design defects go to one fix subagent, with the screenshot names and what to change, before Task 9.

---

### Task 9: Docs

**Files:**
- Modify: `docs/architecture.md`, `README.md`, `docs/superpowers/specs/2026-10-25-milestone-7d-visual-design-design.md`

- [ ] **Step 1: The spec's table**

In the spec's Tokens table:
- set `Accent` to `#C73E24` in both columns, and `Accent.Hover` to `#B23520` in both columns;
- under the table, add one sentence: "The dark accent `#D9452A` gave 4.34:1 with white text; both variants use `#C73E24` (5.07:1)."

In "Facts checked", replace the `Avalonia.Headless.XUnit` bullet with the checked result: "fails test discovery under xunit.v3 4.0.2 (`MissingMethodException`); the tests use `Avalonia.Headless` directly, with a session that is never disposed (its `Dispose` hangs)".

- [ ] **Step 2: Architecture**

`docs/architecture.md`:
- In the roadmap's milestone 7d entry, mark it done the way 7a to 7c are marked. Read how they are marked first.
- In the App section, add one paragraph:
  - the theme lives in `Styles/AppTheme.axaml`, as token brushes for Light and Dark over Fluent, and views use only token keys (a test enforces it);
  - the sidebar is locked while a page runs work, and otherwise leaves every page in the stack through `ILeavable` before `GoHome`;
  - the theme choice is `settings.json`'s `theme`.

- [ ] **Step 3: README**

- In the part that describes the app's screens, add one sentence: the sidebar's theme switch (System, Light, Dark) is saved in `settings.json`.
- In "License", add: "The app ships the Inter font, under the SIL Open Font License 1.1."

- [ ] **Step 4: Commit**

```bash
git add docs README.md
git commit -m "Document the 7d theme, sidebar and keyboard support"
```

---

## Criterion (run by the user, after Task 9)

On the AMD laptop (data root `C:\PS5KTest`, DOOM is PPSA99666), with the Release build, do the steps below twice: once with ☀ Light and once with ☾ Dark chosen in the sidebar. Use the keyboard alone (Tab, Shift+Tab, the arrow keys, Enter, Esc, Space, the Menu key).

1. Home: read This PC, then Tab to the sidebar and open Library.
2. Library: arrow to DOOM, open its actions with the Menu key, choose Settings, change nothing, and come back with Esc.
3. Enter launches DOOM. To see the download card, turn Lavapipe on in Settings first and delete `C:\PS5KTest\tools\mesa`. Then Stop, and Launch again (Enter).
4. Go to Home from the sidebar while DOOM runs: DOOM keeps running.
5. Go through Mode (the arrow keys between the two cards) and back with Esc, without installing.

Each screen matches the screenshots' design, and nothing needs the mouse.
