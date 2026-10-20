using System.Text;

namespace PS5Kitsune.Core.Tests.Games;

/// <summary>A tiny game shaped like DOOM for PS5 (lowbit/ps5-doom v1.1.1): SELF binaries, param.json, icon, data.</summary>
internal static class TestGames
{
    public const string TitleId = "PPSA99666";

    public const string ParamJson = """
        { "titleId": "PPSA99666", "localizedParameters": { "defaultLanguage": "en-US", "en-US": { "titleName": "DOOM" } } }
        """;

    public static void Create(string root)
    {
        foreach (var (name, content) in Files(""))
        {
            TestFiles.Write(Path.Combine(root, name), content);
        }
    }

    /// <summary>The same files as zip entries under <paramref name="prefix"/> ("" for a flat zip, "PPSA99666/" for a top folder).</summary>
    public static byte[] Zip(string prefix) => TestFiles.ZipBytes([.. Files(prefix)]);

    private static IEnumerable<(string Name, byte[] Content)> Files(string prefix) =>
    [
        (prefix + "eboot.bin", TestFiles.Self()),
        (prefix + "sce_module/libc.prx", TestFiles.Self()),
        (prefix + "sce_sys/param.json", Encoding.UTF8.GetBytes(ParamJson)),
        (prefix + "sce_sys/icon0.png", "png"u8.ToArray()),
        (prefix + "wads/doom1.wad", "wad"u8.ToArray()),
    ];
}
