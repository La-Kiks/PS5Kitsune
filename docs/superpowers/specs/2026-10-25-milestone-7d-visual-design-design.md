# Milestone 7d — Visual design: design

October 25, 2026 · Complements [docs/architecture.md](../../architecture.md) (roadmap "7d — Visual design") and the [milestone 7c spec](2026-10-25-milestone-7c-crash-diagnosis-design.md).

## Goal

Give every screen one design, in light and dark: the "Kitsune warm" direction (warm ink and cream paper, with a vermillion accent), the Inter font, a type and spacing scale, and shared components. The window gets a sidebar with Home, Library and a theme switch. The library becomes an icon-first grid. Every screen can be used with the keyboard alone, and selected states reach screen readers. The empty log box during a launch's downloads (a PR #10 follow-up) is replaced.

**Criterion:** every screen follows the same design in both themes, and each one can be used with the keyboard alone. See [Criterion](#criterion).

**Out of scope:**

- Linux font and rendering checks (milestone 8).
- Animations and transitions.
- High contrast beyond what Fluent already does.
- Localization.
- 7c's other follow-ups (see PR #11). Only the empty log box is folded in.
- New navigation behavior beyond the sidebar rule below. Work never continues in the background.

## Decisions

These were made during brainstorming, with mockups in `.superpowers/brainstorm/`:

- **Theme choice:** System, Light or Dark. It is saved in the app settings and applied at once, with no restart.
- **Visual direction:** "Kitsune warm".
- **Shell:** a left sidebar.
- **Sidebar while work runs:** the items are locked. When they are not locked, a click leaves the page the way its own Back does.
- **Library tile:** icon first, with one status pill on the icon.
- **Font:** Inter, embedded.
- **Approach:** keep Fluent's controls, and add a token layer, shared styles and three small custom controls. Rejected: our own ControlThemes for every control (too much work, and every control's accessibility would need checking again), and a third-party theme (a dependency tied to Avalonia releases, with its own look).

## Facts checked on 2026-10-25

- `Avalonia.Fonts.Inter` 12.1.4 exists on NuGet. Its license is MIT; the font inside is OFL 1.1. It depends only on `Avalonia` 12.1.4 and is turned on with `AppBuilder.WithInterFont()`. The CI license check (MIT, BSD, LGPL) accepts it.
- `fonts:Inter#Inter` is the default family, set through `FontManagerOptions.DefaultFamilyName`.
- `Avalonia.Headless.XUnit` 12.1.4 fails test discovery under xunit.v3 4.0.2 (`MissingMethodException`); the tests use `Avalonia.Headless` directly, with a session that is never disposed (its `Dispose` hangs).
- `Avalonia.Skia` 12.1.4 renders headless frames to bitmaps (`UseHeadlessDrawing = false` plus `.UseSkia()`). The screenshots need it.
- Today:
  - `App.axaml` sets Fluent palettes: Palenight dark, Fluent light, accent `#D9452A`.
  - The views hard-code colors: `#C42B1C`, `#9D5D00`, `#0F7B0F`, `#005FB8`, `#40808080`, `#20808080`, `#14808080`, `#616161` and `#2E7D32`.
  - `AppSettings(int SchemaVersion, string? DataRoot)` is at schema version 1.
  - `Navigator` has `GoTo`, `GoBack` and `GoHome(object)`.
  - `IRunningWork` is implemented by `StepRunViewModel` (Install, Add game, Reconvert) and by `ChooseGameViewModel` (while it prepares).
  - `GameViewModel` is not an `IRunningWork`. It downloads while `IsPreparing`, and its status text carries the progress.

## Theme

### Tokens

`src/PS5Kitsune.App/Styles/Tokens.axaml`, merged in `App.axaml`. It holds one `ResourceDictionary.ThemeDictionaries` block, with the same keys under `Light` and `Dark`. Each key is a `SolidColorBrush`, with a `Color` of the same name plus `Color` where a style needs the color itself.

| Key | Dark | Light | Use |
|---|---|---|---|
| `Surface.Page` | `#1E1A18` | `#FAF5EE` | window and page background |
| `Surface.Sidebar` | `#191614` | `#F3ECE2` | sidebar |
| `Surface.Raised` | `#2A2421` | `#FFFFFF` | cards, inputs, log box |
| `Border.Subtle` | `#3A322D` | `#EADFD2` | card and divider borders |
| `Border.Strong` | `#5A4E46` | `#CDBFAF` | hovered card, input border |
| `Text.Primary` | `#F2E9E1` | `#2A211C` | body text |
| `Text.Muted` | `#B3A69C` | `#6E6058` | captions, title IDs |
| `Text.Title` | `#F07A5A` | `#B8381F` | `h1` |
| `Accent` | `#C73E24` | `#C73E24` | primary buttons, selection, focus |
| `Accent.Hover` | `#B23520` | `#B23520` | primary button on hover |
| `OnAccent` | `#FFFFFF` | `#FFFFFF` | text on accent and on status chips |
| `Error` | `#F0806A` | `#B3261E` | error text and the diagnosis border |
| `Warning` | `#E2A24A` | `#8A5300` | warning text (fonts warning) |
| `Status.Green` | `#3E6B3A` | `#2F6B2A` | chip background |
| `Status.Orange` | `#A0661A` | `#94590E` | chip background |
| `Status.Red` | `#B03A22` | `#B3261E` | chip background |
| `Status.Reconvert` | `#8A5A10` | `#8A5300` | chip background |
| `Status.Running` | `#3B6EA8` | `#2D5F96` | chip background |

The dark accent `#D9452A` gave 4.34:1 with white text; both variants use `#C73E24` (5.07:1).

`Status.*` serves Green, Orange, Red, Reconvert and Running. The tool and check chips reuse these keys:

- Installed and Ok use `Status.Green`.
- Missing uses `Border.Strong` with `Text.Primary`.
- Warn uses `Status.Orange`.
- Fail uses `Status.Red`.

**Contrast:** every text and background pair below reaches WCAG AA, 4.5:1, in both variants:

- `Text.Primary`, `Text.Muted` and `Text.Title` on `Surface.Page` and on `Surface.Raised`;
- `Text.Primary` and `Text.Muted` on `Surface.Sidebar`;
- `Error` and `Warning` on `Surface.Page`;
- `OnAccent` on `Accent`, on `Accent.Hover` and on each `Status.*` background.

A test checks it (see [Tests](#tests)). The implementer may adjust a value to pass, keeping its hue; the spec's table is then updated to match.

**Fluent:** the `FluentTheme` palettes in `App.axaml` are set from the same values:

- `Accent` comes from `Accent`.
- `RegionColor` and `AltHigh` come from `Surface.Page`.
- `ChromeLow`, `ChromeMediumLow` and `ChromeAltLow` come from `Surface.Raised`.
- `ChromeMedium` and `ListLow` come from `Border.Subtle`.
- `BaseHigh` comes from `Text.Primary`.

Stock controls (buttons, text boxes, combo boxes, check boxes, flyouts, scroll bars, expanders) then match without their own templates.

### Type and spacing

**Font**
- Add `Avalonia.Fonts.Inter` 12.1.4 to `Directory.Packages.props` and the App project.
- Turn it on with `.WithInterFont()` in `Program.BuildAvaloniaApp`.
- Set a `Window` style with `FontFamily="{StaticResource InterFont}"` (the package's resource), so every page uses it.

**Type classes**, in `src/PS5Kitsune.App/Styles/Typography.axaml`, on `TextBlock`:

| Class | Size | Weight | Brush |
|---|---|---|---|
| `h1` | 22 | SemiBold | `Text.Title` |
| `h2` | 16 | SemiBold | `Text.Primary` |
| (none, body) | 13 | Regular | `Text.Primary` |
| `caption` | 11.5 | Regular | `Text.Muted` |
| `mono` | 12 | Regular | `Text.Primary`, font `Cascadia Mono,Consolas,monospace` |
| `error` | inherits | inherits | `Error` |
| `warning` | inherits | inherits | `Warning` |

**Spacing** (`Styles/Spacing.axaml`):
- `double` resources `Space.1` to `Space.6`: 4, 8, 12, 16, 24 and 32.
- `CornerRadius` resources: `Radius.Small` 4 (chips, inputs), `Radius.Medium` 6 (buttons) and `Radius.Large` 8 (cards).

Pages use these keys rather than literal margins where a value repeats.

### Theme choice

- **Setting:** `AppSettings` gets `AppTheme Theme`, an enum of `System`, `Light` and `Dark`, defaulting to `System`.
  - The record becomes `AppSettings(int SchemaVersion, string? DataRoot, AppTheme Theme = AppTheme.System)`.
  - The schema version stays 1. A file without `theme` reads as `System`, and an unknown value makes the file invalid in the existing way.
  - `AppSettingsStore` serializes the enum as a camelCase string (`"system"`, `"light"`, `"dark"`).
- **`ThemeService`** (App, `src/PS5Kitsune.App/Shell/ThemeService.cs`):
  - It holds the current `AppTheme`.
  - `Apply(AppTheme)` sets `Application.Current.RequestedThemeVariant` to `Default`, `Light` or `Dark`.
  - `ChangeAsync(AppTheme)` applies the theme, then saves it through `AppSettingsStore` (read, modify, write).
  - A failed save is logged as a warning and the theme stays applied.
- **Startup:** `App` applies the saved theme before the window is shown.

## Shell

### Window

`MainWindow.axaml` becomes a two-column grid.

**Sidebar** (168 px, `Surface.Sidebar`, `Border.Subtle` right border), from top to bottom:
- The logo (`Assets/kitsune.ico` rendered at 24 px) and "PS5Kitsune" in `h2`.
- The nav items **Home** and **Library**: `nav` class buttons, full width, left-aligned.
- A spacer.
- The theme switch: three toggle buttons in a row, "◐", "☀" and "☾", with the automation names "System theme", "Light theme" and "Dark theme" and matching tooltips. The current one is checked.

**Page:** `Surface.Page`, 24 px padding, and the navigator's current page.

**Removed:** the header with Platform, Data root and Profile. Home shows them (see [Pages](#pages)).

**Size:** `MinWidth` goes from 600 to 720. `Width` and `Height` stay 860 by 680.

### Sidebar rule

`MainWindowViewModel` gets:

- **`IsBusy`:** true when the current page is an `IRunningWork { IsRunning: true }`, or a `GameViewModel { IsPreparing: true }`. It is raised again whenever the page changes or one of those properties changes.
- **`GoHomeCommand` and `GoLibraryCommand`:** both `CanExecute = !IsBusy`. Each:
  1. awaits `LeaveCurrentPageAsync()`;
  2. calls `Navigator.GoHome(pages.Home())` or `Navigator.GoHome(pages.Library())`.
- **`LeaveCurrentPageAsync()`:**
  - If the page is an `ILeavable`, await `LeaveAsync()`.
  - Else, if it is an `IPreparedGameOwner`, await `ReleaseAsync()`.
- **`CurrentSection`:** `Home`, `Library` or `None`, from the current page's type.
  - `HomeViewModel` maps to `Home`.
  - `LibraryViewModel` maps to `Library`.
  - Every other page maps to `None`: pages inside a flow show no current item.

**New interface:** `src/PS5Kitsune.App/Navigation/ILeavable.cs`, `Task LeaveAsync()`. It runs the page's Back clean-up without navigating.
- `GameViewModel`: the clean-up its "← Library" command does today: cancel the follow and await `Following`. The game keeps running. "← Library" then calls `LeaveAsync()` before it navigates.
- `GameSettingsViewModel`: cancel its exit watch, as its Back does today. Back then calls `LeaveAsync()`.

**Disabled items:** they keep their look at 50% opacity, with the tooltip "Finish or cancel the current step first.". `ToolTip.ShowOnDisabled` is true.

**Current item:**
- A 3 px `Accent` bar on its left edge and `Surface.Raised` behind it (style class `current`).
- `AutomationProperties.ItemStatus="current"`.

**Close handling:** the window's close handling (`NeedsCloseHandling`, `ConfirmCloseAsync`) is unchanged.

### Page frame

Every page uses the same frame, through the `PageFrame` styles. Pages keep their own XAML, laid out as:

- **Top:** the `h1` title, then an optional `caption` line (12 px under the title), then 16 px of space.
- **Middle:** the content, in a `ScrollViewer` when it can grow.
- **Bottom:** the footer, with a 1 px `Border.Subtle` top border, 12 px above, and a 12 px gap between buttons. Secondary actions sit on the left, and Back plus the primary button sit on the right.
  - The primary button has the `accent` class and `IsDefault="True"`.
  - Back has `IsCancel="True"`.

The Library's own "Home" button goes away, since the sidebar replaces it. The Done page keeps "Back to home" as its primary button, and pages inside a flow keep their Back.

## Components

These live in `src/PS5Kitsune.App/Controls/`, with their styles in `src/PS5Kitsune.App/Styles/Controls.axaml`.

### `OptionCard`

- **What it is:** a `RadioButton` subclass with its own `ControlTheme`. Since it stays a `RadioButton`, keyboard handling (the arrow keys within a `GroupName`) and the automation peer (selected state) come from Avalonia.
- **Look:**
  - `Surface.Raised`, a 2 px `Border.Subtle` border, `Radius.Large` and 20 px padding.
  - The content is left- and top-aligned.
- **States:**
  - Hover: `Border.Strong`.
  - Checked: an `Accent` border and a "✓" badge in the top right corner (an `Accent` circle with `OnAccent` text).
  - Disabled: 50% opacity.
  - Keyboard focus: the focus ring (see [Keyboard](#keyboard)).
- **Mode page:**
  - The two `Button.card` become `OptionCard`s in one group. `IsChecked` binds to `IsReleaseSelected` and `IsBuildSelected`, with checking calling the select commands.
  - The visible "Selected" lines go away.
  - The Build card is disabled when `!CanBuild`, as today's command allows.

### `Card` style

- **What it is:** a `Border.card` class: `Surface.Raised`, a 1 px `Border.Subtle` border, `Radius.Large` and 16 px padding.
- **`Border.card.error`:** uses an `Error` border.
- **Used by:**
  - Home's state card;
  - the Tools list;
  - the System check, Install and Reconvert step lists;
  - the game summary;
  - Add result;
  - the diagnosis panel;
  - the Game page's preparing card.

### `StatusChip`

- **What it is:** a `TemplatedControl` with `Kind` (`StatusKind`: Green, Orange, Red, Reconvert, Running, Installed, Missing, Ok, Warn, Fail) and `Text`.
- **Look:**
  - The background comes from the token table above.
  - The text is `OnAccent`, or `Text.Primary` for Missing, at 11 SemiBold.
  - `Radius.Small`, with 6,1 padding.
- **Accessibility:** `AutomationProperties.Name` is bound to `Text`.
- **Replaces:**
  - the Library's colored `Border`s;
  - the Tools page's installed and missing badge;
  - the check rows' status markers, where they use colors today.

### `GameTile`

This is the Library item template, with these parts:

- **Icon:** 136 by 136, `Radius.Large`, clipped. It is the icon image, or the title ID centered in `Text.Muted` on `Surface.Raised` when there is no icon. Its automation name is the game's name.
- **Status pill:** one `StatusChip`, 6 px from the icon's bottom-left corner. It shows the first true of:
  1. `IsRunning` (Running);
  2. `NeedsReconvert` (Reconvert, "Needs reconvert");
  3. `IsRed`, `IsOrange` or `IsGreen`.

  With none true, there is no pill. `GameTileViewModel` gets `StatusKind? Status` and `string? StatusText` for this.
- **Text:**
  - the name, 13 SemiBold, ellipsis;
  - the title ID, in `caption`.
- **"…" button:**
  - top-right on the icon, on `Surface.Raised` with `Radius.Medium`;
  - visible when the item is hovered, has keyboard focus within, or is selected;
  - automation name "Actions for {Name}".
  - It opens the existing `MenuFlyout`, unchanged: Launch, Settings, Open folder, View last log, Reconvert, Clear shader cache, Remove.

## Pages

### Library

- **Header:**
  - the `h1` "Library";
  - the search box (max 320);
  - "Add a game" as the primary button.
  - The "Home" button goes away.
- **Message:** in the `error` class.
- **Empty state:** a `caption`, "No games yet. Add one to start.", and the primary button (unchanged command).
- **Grid:**
  - `ItemsControl` becomes a `ListBox`, with `SelectionMode="Single"`, a `WrapPanel` items panel, `GameTile` items, and item margins of 0,0,16,16.
  - The `ListBoxItem` theme is restyled: no Fluent selection fill. A selected item shows a 2 px `Accent` outline around the icon, and keyboard focus shows the focus ring.
- **Opening a game:**
  - Pressing Enter, or a single click on an item, runs `OpenCommand` with the item. This keeps today's one-click open, and the click also selects the item.
  - The Menu key or Shift+F10 opens the item's flyout. So does the "…" button.
  - This is handled in `LibraryView.axaml.cs`: key handlers that call the view model's commands, with no logic in the view.
- **Automation:** each `ListBoxItem`'s automation name is "{Name}, {TitleId}, {StatusText}", or without the status when there is none. It comes from `GameTileViewModel.AutomationName`.

### Game

- **Header:** the `h1` title.
- **Status lines:**
  - `StatusText` in body text;
  - `FirstLaunchHint` in `caption`;
  - `FontsWarning` in `warning`;
  - `Message` in `error`.
- **Diagnosis panel:**
  - `Border.card.error`;
  - the title in `h2`;
  - the explanation in body text;
  - the matched line in `mono`;
  - the action button in the `accent` class;
  - the result in `caption`.
- **Log area** (the PR #10 follow-up). It replaces the always-visible log box, with three states:
  - **Preparing** (`IsPreparing`): a `Border.card` with `StatusText` and a `ProgressBar`.
    - The bar is determinate when `DownloadFraction` has a value, and indeterminate otherwise.
    - `GameViewModel` gets `double? DownloadFraction`. `ShowDownload` sets it to received ÷ total when the total is known, and to null when waiting or when the total is unknown. It is set back to null when preparation ends.
  - **No lines yet** (`!IsPreparing && Log.Count == 0`): the log box with the `caption` "The game's output appears here." centered. `GameViewModel` gets `bool HasLog`, raised when `Log` changes.
  - **Lines:** the log box (`Surface.Raised`, `Radius.Large`, 8 px padding) with the lines in `mono`. Scrolling to the end is unchanged.
- **Footer:**
  - on the left, "Open folder" and "Settings";
  - on the right, "← Library" (`IsCancel`), "Cancel" while preparing, "Stop" while running, and "Launch again" as the primary button (`IsDefault`) when it can run.

### Home

- **State card:** a `Border.card` with:
  - `StateTitle` in `h2`;
  - the progress bar while checking;
  - `StateDetail` and `OtherInstallText` in body text;
  - a row of buttons: the primary action (accent), "Add a game" and "Library";
  - `AddGameHint` in `caption`.
- **"This PC" section** (new, from the old header): Platform, Data root (with "Change…") and Profile, as a two-column grid of `caption` labels and body values.
  - `HomeViewModel` gets `Platform` and `ProfileText`, using the text the header used: "{Id} (validated)" or "{Id} (not validated yet)". The old profile line merges into this section.
  - The data-root rules line stays a `caption`.
  - `RootError` is in `error`.

### Wizard and add-a-game pages

These are System check, Mode, Tools, Install, Done, Choose game, Game summary, Add game, Add result, Reconvert and Reconvert result. Each gets:

- the page frame;
- cards for its lists and summaries;
- `StatusChip`s where it shows a status;
- the type classes;
- `error` or `warning` for its messages.

Their bindings and commands don't change, except Mode (`OptionCard`).

**Tools page:**
- one `Border.card` with a row per tool, separated by `Border.Subtle` lines;
- each row has its `StatusChip` (Installed or Missing), the name in SemiBold, the version in `caption`, and the size on the right;
- the totals below, in body and `caption`.

### Game settings

The page frame, with each setting in a labelled row. Each label is in body SemiBold, with its explanation in `caption`. The controls are unchanged. `ComboBox` and `CheckBox` take `AutomationProperties.LabeledBy` from their label.

## Keyboard

- **Focus ring:**
  - A `:focus-visible` style on `Button`, `ToggleButton`, `RadioButton` (`OptionCard`), `ListBoxItem`, `TextBox`, `ComboBox`, `CheckBox` and `Expander`'s header.
  - It is a 2 px `Accent` border, 2 px outside the control, drawn with a focus adorner (`FocusAdorner` template), so the control's layout does not move.
  - It shows only after keyboard focus, which is Avalonia's `:focus-visible` behavior.
- **First focus** on each page, set in `OnNavigatedTo` (pages that aren't `INavigationAware` become so). It is applied by the view through a `FocusRequested` event raised by the view model, so view models stay free of controls:
  - Wizard and flow pages: the primary button.
  - Library: the search box when it has games; "Add a game" when it has none.
  - Game: "Launch again" when it can run, then "Stop" when running, then "← Library".
  - Home: the primary action.
  - Settings: the first setting.
- **Enter and Esc:** `IsDefault` on the primary button and `IsCancel` on Back, on every page with a footer.
- **Tab order:** sidebar, then page content, then footer. The Library `ListBox` is one tab stop (`KeyboardNavigation.TabNavigation="Once"`), and the arrow keys move inside it.

## Accessibility

- **`AutomationProperties.Name`** on every control with no visible text:
  - the "…" buttons;
  - the theme toggles;
  - game icon images;
  - progress bars ("Checking", "Download progress", "Install progress", as the page applies);
  - the logo ("PS5Kitsune").
- **Selected states:**
  - `OptionCard` (radio button);
  - the Library `ListBox` (selection);
  - the theme toggles (`ToggleButton.IsChecked`);
  - the sidebar's current item (`ItemStatus`).

  No selected state is shown by style alone.
- **`AutomationProperties.LiveSetting`:**
  - `Assertive` on the diagnosis panel and the `error` messages;
  - `Polite` on `StatusText` lines (Game, Install, Add game, Reconvert).
- **Every `ComboBox` and `CheckBox`** has a name, from its content or `LabeledBy`.

## Errors

- **A theme save that fails** keeps the theme applied for this run. It is logged as a warning: `[LoggerMessage(Level = LogLevel.Warning, Message = "Could not save the theme.")]` with the exception.
- **A page whose `LeaveAsync` throws** during a sidebar click: the exception is logged as a warning (`"Could not leave {Page}."`), and navigation still happens. This matches Back, which cannot fail visibly today.
- **Missing icons** show the title ID, as today.

## Tests

`tests/PS5Kitsune.App.Tests`:

- **View-model tests** (no UI):
  - `MainWindowViewModel`:
    - `IsBusy` follows an `IRunningWork` page and a preparing Game page.
    - `GoLibraryCommand` is disabled while busy.
    - From the Game page, it awaits `LeaveAsync`, then shows the Library with an empty history.
    - From a prepared-game page, it releases the prepared game.
    - `CurrentSection` maps Home, Library and other pages.
  - `ThemeService`: `ChangeAsync` saves the theme and applies it, and a failed save still applies it.
  - `AppSettingsStore`: a file without `theme` reads `System`; `"dark"` round-trips; `"purple"` makes the file invalid.
  - `GameViewModel`:
    - `DownloadFraction` is set from a known total, and null when waiting or the total is unknown;
    - `HasLog` follows `Log`;
    - "← Library" still cancels the follow through `LeaveAsync`.
  - `GameTileViewModel`: the status order (Running, then Reconvert, then compatibility) and `AutomationName`.
  - `HomeViewModel`: `Platform` and `ProfileText`.
- **Theme tests:**
  - **Contrast:** load `Tokens.axaml` and compute the WCAG contrast of each pair listed in [Tokens](#tokens), in both variants; each must be at least 4.5.
  - **No hard-coded colors:** no `#` color literal in `src/PS5Kitsune.App/Views/*.axaml`. Only `Styles/*.axaml` and `App.axaml` may hold colors.
- **Headless tests** (Avalonia.Headless; see [Facts](#facts-checked-on-2026-10-25)):
  - Library:
    - Down and Right move the selection;
    - Enter runs `OpenCommand` with the selected game;
    - Shift+F10 opens the flyout;
    - Tab leaves the list.
  - Mode: the arrow keys check the other `OptionCard`, and its automation peer reports it selected.
  - Window: the sidebar's Library button is disabled while an `IRunningWork` page runs.
  - Every page view: on `OnNavigatedTo`, the expected control has focus.
- **Screenshots:**
  - An opt-in test, `Screenshots.Render_every_page`, renders when `PS5K_SCREENSHOTS` names a folder and is skipped otherwise.
  - It renders every page, with fake data, in Light and Dark, at 860 by 680, to `<folder>\<Page>-<Theme>.png`. The Game page is rendered in its three log states and with a diagnosis.
  - It is used to review the design during the work, and as the PR's UI proof. It is not a pixel comparison.

The existing App tests must still pass after the view model changes. That includes `BackToLibraryCommand` now going through `LeaveAsync`.

## Criterion

On the AMD laptop (data root `C:\PS5KTest`, DOOM is PPSA99666), do these steps twice, once with Light and once with Dark chosen in the sidebar, using only the keyboard (Tab, Shift+Tab, the arrow keys, Enter, Esc, Space, the Menu key):

1. Home: read This PC, then open the Library from the sidebar.
2. Library: move to DOOM, open its actions with the Menu key, open Settings, change nothing, and go back with Esc.
3. Launch DOOM with Enter. While it downloads (delete `tools\mesa` first, if Lavapipe is on, to see the preparing card), watch the progress card. Then Stop, and Launch again.
4. Go to Home from the sidebar while DOOM runs. DOOM keeps running.
5. Go through Mode (the arrow keys between the cards) and back, without installing.

Each screen must match the design of the screenshots, and nothing may need the mouse.

## Docs

- `docs/architecture.md`: roadmap 7d marked done in the milestone list as the others are. In the App section, one paragraph on the theme tokens, the sidebar rule and `ILeavable`.
- `README.md`: the theme switch, and in "License" a line saying the app ships the Inter font under the SIL Open Font License 1.1.
