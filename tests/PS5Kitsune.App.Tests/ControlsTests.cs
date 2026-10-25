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
