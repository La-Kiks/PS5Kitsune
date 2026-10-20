using System.Text.Json;

namespace PS5Kitsune.Core.Games;

public enum AuditVerdict
{
    /// <summary>Nothing absent, no stub: the game can load.</summary>
    Green,

    /// <summary>Stubs only: the game loads but stops if it calls an unimplemented function.</summary>
    Orange,

    /// <summary>Absent imports or a missing library: not playable yet.</summary>
    Red,
}

/// <summary>Unique imports by class, from the audit.json written by import_audit.py --json.</summary>
public sealed record AuditSummary(int Implemented, int Stub, int Absent, IReadOnlyDictionary<string, int> MissingLibraries)
{
    public AuditVerdict Verdict =>
        Absent > 0 || MissingLibraries.Count > 0 ? AuditVerdict.Red
        : Stub > 0 ? AuditVerdict.Orange
        : AuditVerdict.Green;

    /// <exception cref="InvalidDataException">The file is not an import_audit.py result.</exception>
    public static AuditSummary Read(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var counts = root.GetProperty("unique_by_class");
            var missing = root.TryGetProperty("missing_libraries", out var libraries)
                ? libraries.EnumerateObject().ToDictionary(library => library.Name, library => library.Value.GetInt32())
                : new Dictionary<string, int>();
            return new AuditSummary(
                counts.GetProperty("implemented").GetInt32(),
                counts.GetProperty("stub").GetInt32(),
                counts.GetProperty("absent").GetInt32(),
                missing);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{path} is not an import_audit.py result: {exception.Message}", exception);
        }
    }
}
