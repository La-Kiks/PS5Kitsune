using System.Globalization;
using System.Text.RegularExpressions;
using PS5Kitsune.Core.Diagnostics;

namespace PS5Kitsune.Core.Launch;

/// <param name="Line">1 for the first line of the text.</param>
/// <param name="Message">The whole text shown, starting with "Line N: ".</param>
public sealed record ExtraVariableError(int Line, string Message);

/// <param name="Variables">The valid lines, sorted by name; a line with an error is left out.</param>
public sealed record ExtraVariablesParse(IReadOnlyDictionary<string, string> Variables, IReadOnlyList<ExtraVariableError> Errors);

/// <summary>The extra AnyPS5 variables of a game: one NAME=value per line on the Settings page. See the resolution and extra variables spec.</summary>
public static partial class ExtraVariables
{
    /// <summary>Reads the Settings page's text: lines and names trimmed, blank lines skipped, each line split at its first '='.</summary>
    public static ExtraVariablesParse Parse(string? text)
    {
        var variables = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var firstLines = new Dictionary<string, int>(StringComparer.Ordinal);
        var errors = new List<ExtraVariableError>();
        var lines = (text ?? "").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var number = index + 1;
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals < 0)
            {
                errors.Add(Error(number, "write NAME=value."));
                continue;
            }

            var name = line[..equals].Trim();
            if (Problem(name) is { } problem)
            {
                errors.Add(Error(number, problem));
                continue;
            }

            if (firstLines.TryGetValue(name, out var first))
            {
                errors.Add(Error(number, string.Create(CultureInfo.InvariantCulture, $"{name} is already set on line {first}.")));
                continue;
            }

            firstLines[name] = number;
            variables[name] = line[(equals + 1)..].Trim();
        }

        return new ExtraVariablesParse(variables, errors);
    }

    /// <summary>The text the Settings page shows: NAME=value lines sorted by name, joined with '\n'.</summary>
    public static string Format(IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return string.Join('\n', variables.OrderBy(variable => variable.Key, StringComparer.Ordinal).Select(variable => $"{variable.Key}={variable.Value}"));
    }

    /// <summary>Why <paramref name="name"/> cannot be an extra variable, without the "Line N: " start; null when it can.</summary>
    public static string? Problem(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!NamePattern().IsMatch(name))
        {
            return "the name must start with APS5_ or ANYPS5_ and use only capital letters, digits and _.";
        }

        if (LaunchEnvironment.ReservedNames.Contains(name))
        {
            return SettingVariables.Find(name) is { } setting
                ? $"{name} is set by the {setting.Label} setting; use that instead."
                : $"{name} is set by PS5Kitsune itself.";
        }

        return null;
    }

    private static ExtraVariableError Error(int line, string problem) =>
        new(line, string.Create(CultureInfo.InvariantCulture, $"Line {line}: {problem}"));

    [GeneratedRegex(@"^A(?:NY)?PS5_[A-Z0-9_]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
