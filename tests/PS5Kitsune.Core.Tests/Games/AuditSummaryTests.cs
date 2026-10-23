using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class AuditSummaryTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Theory]
    [InlineData(0, 0, "{}", AuditVerdict.Green)]
    [InlineData(5, 0, "{}", AuditVerdict.Orange)]
    [InlineData(5, 2, "{}", AuditVerdict.Red)]
    [InlineData(0, 0, """{ "libSceFoo.prx": 4 }""", AuditVerdict.Red)]
    public void The_verdict_follows_absent_stub_and_missing_libraries(int stub, int absent, string missing, AuditVerdict expected)
    {
        var path = _temp.Combine("audit.json");
        TestFiles.Write(path, AuditFiles.Json(262, stub, absent, missing));

        var summary = AuditSummary.Read(path);

        Assert.Equal(expected, summary.Verdict);
        Assert.Equal((262, stub, absent), (summary.Implemented, summary.Stub, summary.Absent));
    }

    [Fact]
    public void A_file_that_is_not_an_audit_result_is_invalid_data()
    {
        var path = _temp.Combine("audit.json");
        TestFiles.Write(path, """{ "something": "else" }""");

        Assert.Throws<InvalidDataException>(() => AuditSummary.Read(path));
    }

    [Fact]
    public void Lists_the_stub_and_absent_imports_once_each()
    {
        var path = _temp.Combine("audit.json");
        File.WriteAllText(path, AuditFiles.WithImports());

        var summary = AuditSummary.Read(path);

        Assert.Equal(
            [
                new UnresolvedImport("__tls_get_addr_nid_postfix", "libc.prx", AuditClass.Stub),
                new UnresolvedImport("fscanf_nid_postfix", "libSceLibcInternal.prx", AuditClass.Stub),
                new UnresolvedImport("sceSystemServiceLoadExec", "libSceSystemService.prx", AuditClass.Stub),
                new UnresolvedImport("__tls_get_addr_nid_postfix", "libkernel.prx", AuditClass.Stub),
                new UnresolvedImport("AbCdEfGhIjk", "", AuditClass.Absent),
            ],
            summary.Unresolved);
    }

    [Fact]
    public void A_file_without_imports_has_no_unresolved_list()
    {
        var path = _temp.Combine("audit.json");
        File.WriteAllText(path, AuditFiles.Json(262, 0, 0));

        Assert.Empty(AuditSummary.Read(path).Unresolved);
    }

    [Fact]
    public void An_import_without_a_class_is_skipped()
    {
        var path = _temp.Combine("audit.json");
        File.WriteAllText(
            path,
            """
            {
              "unique_by_class": { "implemented": 1, "stub": 1, "absent": 0 },
              "imports": [
                { "name": "noClass" },
                "not an object",
                { "class": 3, "name": "numericClass" },
                { "class": "stub", "name": "validStub", "library": "libkernel.prx" }
              ]
            }
            """);

        var summary = AuditSummary.Read(path);

        Assert.Equal([new UnresolvedImport("validStub", "libkernel.prx", AuditClass.Stub)], summary.Unresolved);
    }

    [Fact]
    public void Imports_that_is_not_an_array_gives_an_empty_list()
    {
        var path = _temp.Combine("audit.json");
        File.WriteAllText(
            path,
            """{ "unique_by_class": { "implemented": 1, "stub": 0, "absent": 0 }, "imports": { "a": 1 } }""");

        Assert.Empty(AuditSummary.Read(path).Unresolved);
    }

    public void Dispose() => _temp.Dispose();
}
