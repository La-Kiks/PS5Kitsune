using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Diagnostics;

/// <summary>
/// Finds the known AnyPS5 error in a launch's log once the game has exited: the first profile rule whose conditions are met,
/// else "unexplained" for a non-zero exit. See the 7c spec, "CrashDiagnoser".
/// </summary>
public sealed partial class CrashDiagnoser
{
    private const int MaxLineLength = 300;

    private readonly IReadOnlyList<CompiledRule> _rules;
    private readonly ILogger<CrashDiagnoser> _logger;

    public CrashDiagnoser(AnyPS5Profile profile, ILogger<CrashDiagnoser> logger)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _logger = logger;
        var rules = new List<CompiledRule>();
        // The loader validated the profile, so this compiles the same rules again without throwing.
        foreach (var rule in DiagnosticRules.Compile(profile.Id, profile.Diagnostics))
        {
            if (DiagnosisIds.Known.Contains(rule.Rule.Id))
            {
                rules.Add(rule);
            }
            else
            {
                var id = rule.Rule.Id;
                LogUnknownRule(id);
            }
        }

        _rules = rules;
    }

    /// <summary>Appends the exit line to the log, then diagnoses it unless the user stopped the game.</summary>
    public async Task<Diagnosis?> FinishAsync(string logPath, int exitCode, bool stopped, CancellationToken cancellationToken)
    {
        await AppendExitLineAsync(logPath, exitCode, stopped).ConfigureAwait(false);
        return stopped ? null : await DiagnoseAsync(logPath, (uint)exitCode, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Appends the exit line on a line of its own; a failed write is logged and changes nothing else.</summary>
    public async Task AppendExitLineAsync(string logPath, int exitCode, bool stopped)
    {
        ArgumentNullException.ThrowIfNull(logPath);
        var line = stopped ? ExitLines.Stopped(exitCode) : ExitLines.Exited(exitCode);
        try
        {
            var stream = new FileStream(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            await using (stream.ConfigureAwait(false))
            {
                var separator = "";
                if (stream.Length > 0)
                {
                    stream.Seek(-1, SeekOrigin.End);
                    separator = stream.ReadByte() == '\n' ? "" : Environment.NewLine;
                }

                stream.Seek(0, SeekOrigin.End);
                await stream.WriteAsync(Encoding.UTF8.GetBytes(separator + line + Environment.NewLine)).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogExitLineFailed(exception, logPath);
        }
    }

    /// <param name="exitCode">The exit code; null to use the log's exit line, when it has one.</param>
    /// <returns>
    /// The first rule met, in profile order; else "unexplained" for a non-zero exit code; else null (a zero or unknown exit code, or a game
    /// the user stopped). An unreadable log or a regex that times out is logged and gives null.
    /// </returns>
    public async Task<Diagnosis?> DiagnoseAsync(string logPath, uint? exitCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logPath);
        try
        {
            return await Task.Run(() => Diagnose(logPath, exitCode, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
        {
            LogDiagnosisFailed(exception, logPath);
            return null;
        }
    }

    private Diagnosis? Diagnose(string logPath, uint? exitCode, CancellationToken cancellationToken)
    {
        var states = _rules.Select(rule => new RuleState(rule)).ToList();
        uint? loggedCode = null;
        var stoppedSeen = false;
        using (var reader = new StreamReader(new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)))
        {
            var number = 0;
            while (reader.ReadLine() is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                number++;
                if (ExitLines.Parse(line) is { } parsed)
                {
                    loggedCode = parsed.Code;
                    stoppedSeen |= parsed.Stopped;
                    continue;
                }

                foreach (var state in states)
                {
                    state.See(line, number);
                }
            }
        }

        if (exitCode is null && stoppedSeen)
        {
            return null;
        }

        var code = exitCode ?? loggedCode;
        foreach (var state in states)
        {
            if (state.IsMet(code))
            {
                return state.Diagnosis(code, logPath);
            }
        }

        return code is { } failed and not 0
            ? new Diagnosis(DiagnosisIds.Unexplained, new Dictionary<string, string>(), null, null, failed, logPath)
            : null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The profile's diagnostics rule {Id} is not known to this app: it is skipped.")]
    private partial void LogUnknownRule(string id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not add the exit line to {LogPath}.")]
    private partial void LogExitLineFailed(Exception exception, string logPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not diagnose {LogPath}: no diagnosis is shown.")]
    private partial void LogDiagnosisFailed(Exception exception, string logPath);

    // What one rule has seen so far: which of its regexes matched, their named groups, and the first matched line.
    private sealed class RuleState(CompiledRule rule)
    {
        private readonly bool[] _matched = new bool[rule.Log.Count];
        private readonly Dictionary<string, string> _captures = new(StringComparer.Ordinal);
        private string? _line;
        private int? _lineNumber;

        public void See(string line, int number)
        {
            for (var index = 0; index < rule.Log.Count; index++)
            {
                if (_matched[index] || rule.Log[index].Match(line) is not { Success: true } match)
                {
                    continue;
                }

                _matched[index] = true;
                foreach (Group group in match.Groups)
                {
                    // Numbered groups are named "0", "1"…: only named groups are captures.
                    if (group.Success && group.Value.Length > 0 && !char.IsAsciiDigit(group.Name[0]))
                    {
                        _captures.TryAdd(group.Name, group.Value.Trim());
                    }
                }

                if (_line is null)
                {
                    var text = line.Trim();
                    _line = text.Length > MaxLineLength ? text[..MaxLineLength] : text;
                    _lineNumber = number;
                }
            }
        }

        public bool IsMet(uint? code)
        {
            var codeMatches = code is { } value && rule.ExitCodes.Contains(value);
            return rule.MatchesAll
                ? _matched.All(matched => matched) && (rule.ExitCodes.Count == 0 || codeMatches)
                : _matched.Any(matched => matched) || codeMatches;
        }

        public Diagnosis Diagnosis(uint? code, string logPath)
        {
            var captures = new Dictionary<string, string>(_captures, StringComparer.Ordinal);
            foreach (var (name, value) in rule.Rule.Captures)
            {
                captures.TryAdd(name, value);
            }

            return new Diagnosis(rule.Rule.Id, captures, _line, _lineNumber, code, logPath);
        }
    }
}
