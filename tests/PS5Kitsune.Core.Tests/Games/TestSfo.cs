using System.Buffers.Binary;
using System.Text;

namespace PS5Kitsune.Core.Tests.Games;

/// <summary>Writes param.sfo files in the PSF layout PS4 dumps use: header, entries, key table, data table.</summary>
internal static class TestSfo
{
    public const ushort TextWithNul = 0x0204;

    public const ushort Text = 0x0004;

    public const ushort Number = 0x0404;

    public static byte[] Bloodborne { get; } = Texts(("APP_VER", "01.09"), ("TITLE", "Bloodborne®"), ("TITLE_ID", "CUSA03173"));

    /// <summary>Each value as UTF-8 text with a NUL and padding to 4 bytes, format 0x0204.</summary>
    public static byte[] Texts(params (string Key, string Value)[] texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return Build([.. texts.Select(text => (text.Key, TextWithNul, Padded(text.Value)))]);
    }

    public static byte[] Build(params (string Key, ushort Format, byte[] Data)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var table = new List<byte>();
        var keys = new List<byte>();
        var data = new List<byte>();
        foreach (var (key, format, value) in entries)
        {
            var entry = new byte[16];
            BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(0), (ushort)keys.Count);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(2), format);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(4), (uint)value.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(8), (uint)value.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(12), (uint)data.Count);
            table.AddRange(entry);
            keys.AddRange(Encoding.ASCII.GetBytes(key));
            keys.Add(0);
            data.AddRange(value);
        }

        while (keys.Count % 4 != 0)
        {
            keys.Add(0);
        }

        var header = new byte[20];
        "\0PSF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), 0x0101);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), (uint)(20 + table.Count));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), (uint)(20 + table.Count + keys.Count));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), (uint)entries.Length);
        return [.. header, .. table, .. keys, .. data];
    }

    private static byte[] Padded(string value)
    {
        var bytes = new List<byte>(Encoding.UTF8.GetBytes(value)) { 0 };
        while (bytes.Count % 4 != 0)
        {
            bytes.Add(0);
        }

        return [.. bytes];
    }
}
