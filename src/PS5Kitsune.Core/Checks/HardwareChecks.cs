using System.Globalization;
using System.Runtime.InteropServices;

namespace PS5Kitsune.Core.Checks;

public sealed class OsArchitectureCheck : SystemCheck
{
    public override string Id => "os-arch";

    public override string Title => "64-bit x86 system";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.OSArchitecture == Architecture.X64
            ? Result(CheckStatus.Ok, "64-bit x86 system.")
            : Result(CheckStatus.Blocking, $"This is an {facts.OSArchitecture} system; AnyPS5 produces x86-64 code only.", "Use a PC with a 64-bit Intel or AMD processor.");
    }
}

public sealed class CpuExtensionsCheck : SystemCheck
{
    public override string Id => "cpu-extensions";

    public override string Title => "CPU instruction sets";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var missing = facts.Cpu.MissingExtensions;
        return missing.Count == 0
            ? Result(CheckStatus.Ok, $"{string.Join(", ", X86CpuInfo.RequiredExtensions)} are present.")
            : Result(CheckStatus.Blocking, $"Missing: {string.Join(", ", missing)}. A converted game stops at startup without them.", "Use a PC with a newer processor: Intel 4th generation (Haswell) or AMD Ryzen and later.");
    }
}

public sealed class CpuVendorCheck : SystemCheck
{
    public override string Id => "cpu-vendor";

    public override string Title => "CPU vendor";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.Cpu.Vendor switch
        {
            "GenuineIntel" => Result(CheckStatus.Info, "Intel processor: games are converted with --to-intel. Games that generate AMD instructions while running still need an AMD processor."),
            "AuthenticAMD" => Result(CheckStatus.Info, "AMD processor: games are converted without --to-intel."),
            var vendor => Result(CheckStatus.Info, $"Processor vendor \"{vendor}\": games are converted without --to-intel."),
        };
    }
}

public sealed class CommitLimitCheck : SystemCheck
{
    private const ulong MiB = 1 << 20;

    public override string Id => "commit-limit";

    public override string Title => "Memory commit limit (RAM + page file)";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.CommitLimitBytes is not { } bytes)
        {
            return Result(CheckStatus.Info, "Not applicable on this system.");
        }

        var available = bytes / MiB;
        var needed = (ulong)facts.Limits.CommitLimitMiB;
        return available >= needed
            ? Result(CheckStatus.Ok, string.Create(CultureInfo.InvariantCulture, $"{available:N0} MiB available; a game can commit up to {needed:N0} MiB."))
            : Result(
                CheckStatus.Warning,
                string.Create(CultureInfo.InvariantCulture, $"RAM + page file allow {available:N0} MiB; a game can commit up to {needed:N0} MiB and may crash with less."),
                string.Create(CultureInfo.InvariantCulture, $"Enlarge the page file until RAM + page file reach {needed:N0} MiB: Performance Options > Advanced > Virtual memory > Change."),
                CheckFix.OpenPerformanceSettings);
    }
}
