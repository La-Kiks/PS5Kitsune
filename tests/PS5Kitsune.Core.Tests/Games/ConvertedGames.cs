using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

/// <summary>A game folder as an add leaves it, after the game ran once: conversion outputs, and the files Reconvert must never touch.</summary>
internal static class ConvertedGames
{
    /// <summary>Files the game or the user created, relative to the game folder.</summary>
    public static readonly string[] Untouched =
    [
        "data/save.dat", "download0/dlc.bin", "_trophy/trophy.dat", "anyps5-input.ini", "anyps5-fonts/font.ttf", "shader_cache/v1/cache.bin",
        "app0/eboot.bin", "app0/sce_module/libc.prx", "app0/sce_sys/param.json", ".ps5kitsune/originals/eboot.bin", ".ps5kitsune/logs/2026-10-23.log",
    ];

    public static void Create(GameLayout game)
    {
        foreach (var relative in Untouched)
        {
            TestFiles.Write(Path.Combine(game.Root, relative), relative == "app0/eboot.bin" || relative == "app0/sce_module/libc.prx" ? TestFiles.Elf() : System.Text.Encoding.UTF8.GetBytes("kept " + relative));
        }

        TestFiles.Write(game.Executable, "old exe");
        TestFiles.Write(game.Registry, "old registry");
        TestFiles.Write(Path.Combine(game.Root, "PPSA99666.libc.registry.json"), "old module registry");
        TestFiles.Write(Path.Combine(game.Root, "PPSA99666.needed.json"), "old needed");
        TestFiles.Write(game.AuditFile, AuditFiles.Json(360, 4, 0));
        TestFiles.Write(game.RelinkedMarker, "");
        TestFiles.Write(Path.Combine(game.Libs, "libSceAudioOut.prx"), "old prx");
        TestFiles.Write(Path.Combine(game.Libs, "libOnlyInOldVersion.prx"), "old only");
        TestFiles.Write(Path.Combine(game.App0, "sce_module", "libc.guest.prx"), "old pe");
    }

    /// <summary>Every file under <paramref name="root"/> with its content in hex, by '/' relative path.</summary>
    public static SortedDictionary<string, string> Snapshot(string root) => new(
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(root, file).Replace('\\', '/'), file => Convert.ToHexString(File.ReadAllBytes(file))),
        StringComparer.Ordinal);

    /// <summary>True for a conversion output, or a file Reconvert rewrites (game.json, the audit's module copies).</summary>
    public static bool IsConversionOutput(string relative) =>
        relative.EndsWith(".exe", StringComparison.Ordinal)
        || relative.EndsWith(".registry.json", StringComparison.Ordinal)
        || relative.EndsWith(".needed.json", StringComparison.Ordinal)
        || relative.EndsWith(".guest.prx", StringComparison.Ordinal)
        || relative is "audit.json" or ".ps5kitsune/relinked" or ".ps5kitsune/game.json"
        || relative.StartsWith("libs/", StringComparison.Ordinal)
        || relative.StartsWith(".ps5kitsune/audit-modules/", StringComparison.Ordinal);
}
