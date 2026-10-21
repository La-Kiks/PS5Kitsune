using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.App.Converters;

/// <summary>Badge and banner color of a check status; gray while checking (null).</summary>
public sealed class CheckStatusBrushConverter : IValueConverter
{
    public static CheckStatusBrushConverter Instance { get; } = new();

    private static readonly IBrush Ok = new ImmutableSolidColorBrush(Color.Parse("#2E7D32"));
    private static readonly IBrush Info = new ImmutableSolidColorBrush(Color.Parse("#1565C0"));
    private static readonly IBrush Warning = new ImmutableSolidColorBrush(Color.Parse("#E65100"));
    private static readonly IBrush Blocking = new ImmutableSolidColorBrush(Color.Parse("#C62828"));
    private static readonly IBrush Pending = new ImmutableSolidColorBrush(Color.Parse("#616161"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        CheckStatus.Ok => Ok,
        CheckStatus.Info => Info,
        CheckStatus.Warning => Warning,
        CheckStatus.Blocking => Blocking,
        _ => Pending,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
