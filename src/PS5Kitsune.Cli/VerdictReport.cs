using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

/// <summary>The audit counts and the verdict line, as add and reconvert print them.</summary>
internal static class VerdictReport
{
    public static void Print(ILogger logger, AuditRecord audit)
    {
        var implemented = audit.Implemented;
        var stub = audit.Stub;
        var absent = audit.Absent;
        logger.AuditCounts(implemented, stub, absent);
        switch (audit.Verdict)
        {
            case AuditVerdict.Green:
                logger.VerdictGreen();
                break;
            case AuditVerdict.Orange:
                logger.VerdictOrange();
                break;
            default:
                logger.VerdictRed();
                break;
        }
    }
}
