using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.App.Views;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Profiles;
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

    [Fact]
    public Task Mode_cards_are_radio_buttons_the_arrow_keys_move_between() => session.RunAsync(() =>
    {
        var profile = TestSessions.Profile with
        {
            Build = new ProfileBuild(new Uri("https://github.com/boykopovar/AnyPS5.git"), "3fd1cc393b8512c2e989860053f30dee5a8cd1aa"),
            Checks = new CheckThresholds { BuildMinFreeDiskGiB = 10 },
        };
        var viewModel = new ModeViewModel(profile, new FakeInstaller(), new Navigator(), new FakePages());
        var view = new ModeView { DataContext = viewModel };
        var window = Show(view);
        try
        {
            var cards = view.GetVisualDescendants().OfType<RadioButton>().ToList();
            Assert.Equal(2, cards.Count);
            cards[0].Focus(NavigationMethod.Tab);

            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);

            Assert.Equal(AnyPS5Mode.Build, viewModel.SelectedMode);
            Assert.True(cards[1].IsChecked);
        }
        finally
        {
            window.Close();
        }
    });

    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }
}
