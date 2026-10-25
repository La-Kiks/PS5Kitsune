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
