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
