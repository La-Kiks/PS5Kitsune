using System.Globalization;
using System.Text.RegularExpressions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Diagnostics;

/// <summary>A profile rule with its regexes compiled and its exit codes parsed.</summary>
internal sealed record CompiledRule(DiagnosticRule Rule, IReadOnlyList<Regex> Log, IReadOnlySet<uint> ExitCodes)
{
    public bool MatchesAll => Rule.Match == DiagnosticRule.MatchAll;
}

internal static class DiagnosticRules
{
    /// <summary>How long one regex may run on one line.</summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    /// <exception cref="InvalidProfileException">
    /// The report URL is not HTTPS, or a rule has no id, no condition, an unknown match mode, a regex that does not compile or an exit code that is not hex.
    /// </exception>
    public static IReadOnlyList<CompiledRule> Compile(string profileId, ProfileDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (diagnostics.ReportUrl is { } url && url.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidProfileException($"Profile {profileId}: diagnostics.reportUrl must use HTTPS.");
        }

        var compiled = new List<CompiledRule>();
        foreach (var rule in diagnostics.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                throw new InvalidProfileException($"Profile {profileId}: a diagnostics rule has no id.");
            }

            if (rule.Log.Count == 0 && rule.ExitCodes.Count == 0)
            {
                throw new InvalidProfileException($"Profile {profileId}: diagnostics rule {rule.Id} needs log patterns or exit codes.");
            }

            if (rule.Match is not (DiagnosticRule.MatchAll or DiagnosticRule.MatchAny))
            {
                throw new InvalidProfileException($"Profile {profileId}: diagnostics rule {rule.Id} has match \"{rule.Match}\"; use \"all\" or \"any\".");
            }

            var regexes = rule.Log.Select(pattern => CompileRegex(profileId, rule.Id, pattern)).ToList();
            var codes = rule.ExitCodes.Select(code => ParseExitCode(profileId, rule.Id, code)).ToHashSet();
            compiled.Add(new CompiledRule(rule, regexes, codes));
        }

        return compiled;
    }

    private static Regex CompileRegex(string profileId, string id, string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, MatchTimeout);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidProfileException($"Profile {profileId}: diagnostics rule {id} has an invalid pattern \"{pattern}\": {exception.Message}", exception);
        }
    }

    private static uint ParseExitCode(string profileId, string id, string code)
    {
        if (code.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && uint.TryParse(code.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new InvalidProfileException($"Profile {profileId}: diagnostics rule {id} has exit code \"{code}\"; use hex such as 0xC000001D.");
    }
}
