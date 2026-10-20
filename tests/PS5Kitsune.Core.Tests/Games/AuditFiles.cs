namespace PS5Kitsune.Core.Tests.Games;

/// <summary>The part of import_audit.py's --json output that PS5Kitsune reads (AnyPS5 v0.1.2).</summary>
internal static class AuditFiles
{
    public static string Json(int implemented, int stub, int absent, string missingLibraries = "{}") => $$"""
        {
          "source_commit": null,
          "references": 400,
          "unique_imports": {{implemented + stub + absent}},
          "unique_by_class": { "implemented": {{implemented}}, "stub": {{stub}}, "absent": {{absent}}, "module": 0 },
          "missing_libraries": {{missingLibraries}},
          "imports": []
        }
        """;
}
