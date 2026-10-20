namespace PS5Kitsune.Core.Games;

public enum BinaryKind
{
    Missing,
    Elf,
    Self,
    Other,
}

public static class BinaryKinds
{
    /// <summary>
    /// ELF: 7F 45 4C 46. SELF: 4F 15 3D 1D or 54 14 F5 EE, the two magics unwrap_self.py accepts.
    /// </summary>
    public static BinaryKind Detect(string path)
    {
        if (!File.Exists(path))
        {
            return BinaryKind.Missing;
        }

        Span<byte> magic = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        if (stream.ReadAtLeast(magic, magic.Length, throwOnEndOfStream: false) < magic.Length)
        {
            return BinaryKind.Other;
        }

        return magic switch
        {
            [0x7F, 0x45, 0x4C, 0x46] => BinaryKind.Elf,
            [0x4F, 0x15, 0x3D, 0x1D] or [0x54, 0x14, 0xF5, 0xEE] => BinaryKind.Self,
            _ => BinaryKind.Other,
        };
    }
}
