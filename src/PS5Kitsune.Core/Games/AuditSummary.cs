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

/// <summary>The class of an import the game needs and AnyPS5 does not implement.</summary>
public enum AuditClass
{
    Stub,
    Absent,
}

/// <param name="Name">The function's name, or its NID when the audit has no name.</param>
/// <param name="Where">The library, else the module, that the game imports it from; "" when the audit gives neither.</param>
public sealed record UnresolvedImport(string Name, string Where, AuditClass Class);

/// <summary>Unique imports by class, from the audit.json written by import_audit.py --json.</summary>
public sealed record AuditSummary(int Implemented, int Stub, int Absent, IReadOnlyDictionary<string, int> MissingLibraries)
{
    /// <summary>The stub and absent entries of audit.json's imports, in file order, each name and place once.</summary>
    public IReadOnlyList<UnresolvedImport> Unresolved { get; init; } = [];

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
            var unresolved = new List<UnresolvedImport>();
            if (root.TryGetProperty("imports", out var imports) && imports.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in imports.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    AuditClass? kind = Text(entry, "class") switch
                    {
                        "stub" => AuditClass.Stub,
                        "absent" => AuditClass.Absent,
                        _ => null,
                    };
                    if (kind is not { } auditClass)
                    {
                        continue;
                    }

                    var import = new UnresolvedImport(
                        Text(entry, "name") ?? Text(entry, "nid") ?? "",
                        Text(entry, "library") ?? Text(entry, "module") ?? "",
                        auditClass);
                    if (!unresolved.Contains(import))
                    {
                        unresolved.Add(import);
                    }
                }
            }

            return new AuditSummary(
                counts.GetProperty("implemented").GetInt32(),
                counts.GetProperty("stub").GetInt32(),
                counts.GetProperty("absent").GetInt32(),
                missing)
            {
                Unresolved = unresolved,
            };
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{path} is not an import_audit.py result: {exception.Message}", exception);
        }
    }

    private static string? Text(JsonElement entry, string property) =>
        entry.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
