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

    public void Dispose() => _temp.Dispose();
}
