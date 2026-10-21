namespace PS5Kitsune.Core.AnyPS5.Build;

/// <summary>Jobs for cmake --build: GCC can use about 2 GiB per job on large C++ files, so memory caps the cores.</summary>
public static class ParallelJobs
{
    private const long BytesPerJob = 2L << 30;

    public static int Compute(int logicalProcessors, long physicalMemoryBytes) =>
        Math.Max(1, Math.Min(logicalProcessors, (int)(physicalMemoryBytes / BytesPerJob)));

    public static int ForThisMachine() =>
        Compute(Environment.ProcessorCount, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
}
