namespace PS5Kitsune.Core.IO;

internal static class FileTree
{
    /// <summary>Every file under <paramref name="root"/>, relative to it; nothing when the folder does not exist.</summary>
    public static IEnumerable<string> RelativeFiles(string root) =>
        Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(root, path))
            : [];

    /// <summary>Total size in bytes of the files under <paramref name="root"/>.</summary>
    public static long Size(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);

    /// <summary>True when <paramref name="copy"/> exists with the size of <paramref name="source"/>.</summary>
    public static bool SameSize(string source, string copy) =>
        File.Exists(copy) && new FileInfo(source).Length == new FileInfo(copy).Length;

    /// <summary>Copies every file of <paramref name="sourceRoot"/> for which <paramref name="isCopied"/> is false.</summary>
    public static void Copy(string sourceRoot, string targetRoot, Func<string, bool> isCopied, CancellationToken cancellationToken)
    {
        foreach (var relative in RelativeFiles(sourceRoot).ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (isCopied(relative))
            {
                continue;
            }

            var target = Path.Combine(targetRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(sourceRoot, relative), target, overwrite: true);
        }
    }
}
