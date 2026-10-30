using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Platform.Linux;

/// <summary>Finds a program the way a shell does: the first executable file of that name in the PATH folders.</summary>
internal static class PathSearch
{
    public static string? Find(string name, string? path)
    {
        foreach (var folder in (path ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Path.IsPathRooted(folder))
            {
                continue;
            }

            var candidate = Path.Combine(folder, name);
            if (File.Exists(candidate) && ExecutableBits.Has(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
