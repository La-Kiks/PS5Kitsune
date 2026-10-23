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

    /// <summary>import_audit.py --json output with an imports list: the 4 stub entries of DOOM for PS5 v1.1.1, one duplicated, one implemented.</summary>
    public static string WithImports() => """
        {
          "unique_by_class": { "implemented": 360, "stub": 4, "absent": 0, "module": 0 },
          "missing_libraries": {},
          "imports": [
            { "nid": "+BzXYkqYeLE", "library": null, "module": "libc.prx", "class": "implemented", "name": null },
            { "nid": "vNe1w4diLCs", "library": null, "module": "libc.prx", "class": "stub", "name": "__tls_get_addr_nid_postfix" },
            { "nid": "npLpPTaSuHg", "library": "libSceLibcInternal.prx", "class": "stub", "name": "fscanf_nid_postfix" },
            { "nid": "JoBqSQt1yyA", "library": "libSceSystemService.prx", "class": "stub", "name": "sceSystemServiceLoadExec" },
            { "nid": "vNe1w4diLCs", "library": "libkernel.prx", "class": "stub", "name": "__tls_get_addr_nid_postfix" },
            { "nid": "JoBqSQt1yyA", "library": "libSceSystemService.prx", "class": "stub", "name": "sceSystemServiceLoadExec" },
            { "nid": "AbCdEfGhIjk", "library": null, "class": "absent", "name": null }
          ]
        }
        """;
}
