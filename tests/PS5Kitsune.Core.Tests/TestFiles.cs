using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace PS5Kitsune.Core.Tests;

/// <summary>Builds the small files the tests need: ELF and SELF stand-ins, text files, zips.</summary>
internal static class TestFiles
{
    /// <summary>64 bytes starting with the ELF magic 7F 45 4C 46.</summary>
    public static byte[] Elf() => [0x7F, 0x45, 0x4C, 0x46, .. new byte[60]];

    /// <summary>128 bytes starting with the SELF magic 4F 15 3D 1D; a different size from <see cref="Elf"/>.</summary>
    public static byte[] Self() => [0x4F, 0x15, 0x3D, 0x1D, .. new byte[124]];

    public static void Write(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    public static void Write(string path, string content) => Write(path, Encoding.UTF8.GetBytes(content));

    public static byte[] ZipBytes(params (string Name, byte[] Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(content);
            }
        }

        return buffer.ToArray();
    }

    public static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
