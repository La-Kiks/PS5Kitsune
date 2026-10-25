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
