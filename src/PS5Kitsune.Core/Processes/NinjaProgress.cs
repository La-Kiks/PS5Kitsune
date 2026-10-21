using System.Globalization;
using System.Text.RegularExpressions;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Processes;

/// <summary>Reads Ninja's status lines, "[123/2400] Building CXX object …".</summary>
public static partial class NinjaProgress
{
    public static bool TryParse(string line, out StepFraction fraction)
    {
        ArgumentNullException.ThrowIfNull(line);
        fraction = default;
        var match = StatusPattern().Match(line);
        if (!match.Success
            || !int.TryParse(match.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var done)
            || !int.TryParse(match.Groups[2].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var total)
            || total <= 0 || done > total)
        {
            return false;
        }

        fraction = new StepFraction(done, total);
        return true;
    }

    [GeneratedRegex(@"^\[(\d+)/(\d+)\]")]
    private static partial Regex StatusPattern();
}
