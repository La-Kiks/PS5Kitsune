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
            if (kind == Kind)
            {
                PseudoClasses.Add(name);
            }
            else
            {
                PseudoClasses.Remove(name);
            }
        }
    }
}
