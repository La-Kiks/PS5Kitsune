using System.Buffers.Binary;
using System.Text;

namespace PS5Kitsune.Core.Games;

/// <summary>
/// Reads the title ID and name from a PS4 game's sce_sys/param.sfo (PSF layout, little-endian). See the PS4 detection spec.
/// </summary>
internal static class ParamSfo
{
    private const int HeaderSize = 20;
    private const int EntrySize = 16;
    private const ushort TextWithNul = 0x0204;
    private const ushort Text = 0x0004;
    private const long MaxSize = 64 * 1024;
    private const string NotPsf = "sce_sys/param.sfo is not a valid PSF file.";

    /// <summary>PS4 title IDs start with CUSA; PS5 ones with PPSA.</summary>
    public static bool IsPs4TitleId(string titleId) => titleId.StartsWith("CUSA", StringComparison.Ordinal);

    /// <exception cref="InvalidDataException">The file is not a PSF file with a usable TITLE_ID; the message says why.</exception>
    public static GameMetadata Read(string path)
    {
        if (new FileInfo(path).Length > MaxSize)
        {
            throw new InvalidDataException(NotPsf);
        }

        var texts = ReadTexts(File.ReadAllBytes(path));
        if (!texts.TryGetValue("TITLE_ID", out var titleId) || string.IsNullOrWhiteSpace(titleId))
        {
            throw new InvalidDataException("sce_sys/param.sfo has no TITLE_ID.");
        }

        if (!GameValidator.IsValidTitleId(titleId))
        {
            throw new InvalidDataException($"Title ID \"{titleId}\" must contain only letters and digits.");
        }

        var name = texts.TryGetValue("TITLE", out var title) && !string.IsNullOrWhiteSpace(title) ? title : titleId;
        return new GameMetadata(titleId, name, IsPs4: true);
    }

    // Every text entry by key; text stops at the first NUL.
    private static Dictionary<string, string> ReadTexts(byte[] bytes)
    {
        if (bytes.Length < HeaderSize || !bytes.AsSpan(0, 4).SequenceEqual("\0PSF"u8))
        {
            throw new InvalidDataException(NotPsf);
        }

        long keyTable = U32(bytes, 8);
        long dataTable = U32(bytes, 12);
        long count = U32(bytes, 16);
        if (HeaderSize + (count * EntrySize) > bytes.Length)
        {
            throw new InvalidDataException(NotPsf);
        }

        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            var entry = HeaderSize + (index * EntrySize);
            var format = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(entry + 2));
            if (format is not (TextWithNul or Text))
            {
                continue;
            }

            var key = Key(bytes, keyTable + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(entry)));
            long length = U32(bytes, entry + 4);
            var start = dataTable + U32(bytes, entry + 12);
            if (start + length > bytes.Length)
            {
                throw new InvalidDataException(NotPsf);
            }

            var data = bytes.AsSpan((int)start, (int)length);
            var nul = data.IndexOf((byte)0);
            texts[key] = Encoding.UTF8.GetString(nul < 0 ? data : data[..nul]);
        }

        return texts;
    }

    private static string Key(byte[] bytes, long start)
    {
        if (start >= bytes.Length)
        {
            throw new InvalidDataException(NotPsf);
        }

        var end = bytes.AsSpan((int)start).IndexOf((byte)0);
        if (end < 0)
        {
            throw new InvalidDataException(NotPsf);
        }

        return Encoding.ASCII.GetString(bytes, (int)start, end);
    }

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}
