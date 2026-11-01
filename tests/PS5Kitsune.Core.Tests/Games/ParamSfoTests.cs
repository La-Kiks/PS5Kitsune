using System.Buffers.Binary;
using System.Text;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class ParamSfoTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private GameMetadata Read(byte[] bytes)
    {
        var path = _temp.Combine("param.sfo");
        File.WriteAllBytes(path, bytes);
        return ParamSfo.Read(path);
    }

    private string ReadError(byte[] bytes) => Assert.Throws<InvalidDataException>(() => Read(bytes)).Message;

    [Fact]
    public void The_title_id_and_the_utf8_name_are_read_and_marked_ps4() =>
        Assert.Equal(new GameMetadata("CUSA03173", "Bloodborne®", IsPs4: true), Read(TestSfo.Bloodborne));

    [Fact]
    public void Text_without_a_nul_is_read() =>
        Assert.Equal(
            new GameMetadata("CUSA01664", "LIMBO", IsPs4: true),
            Read(TestSfo.Build(("TITLE", TestSfo.Text, Encoding.UTF8.GetBytes("LIMBO")), ("TITLE_ID", TestSfo.Text, Encoding.UTF8.GetBytes("CUSA01664")))));

    [Fact]
    public void Number_entries_are_skipped() =>
        Assert.Equal("CUSA03173", Read(TestSfo.Build(("PARENTAL_LEVEL", TestSfo.Number, [5, 0, 0, 0]), ("TITLE_ID", TestSfo.Text, "CUSA03173"u8.ToArray()))).TitleId);

    [Fact]
    public void A_missing_or_empty_title_falls_back_to_the_title_id()
    {
        Assert.Equal("CUSA03173", Read(TestSfo.Texts(("TITLE_ID", "CUSA03173"))).Name);
        Assert.Equal("CUSA03173", Read(TestSfo.Texts(("TITLE", ""), ("TITLE_ID", "CUSA03173"))).Name);
    }

    [Fact]
    public void A_bad_magic_is_not_a_psf_file()
    {
        var bytes = TestSfo.Bloodborne.ToArray();
        bytes[1] = (byte)'X';

        Assert.Equal("sce_sys/param.sfo is not a valid PSF file.", ReadError(bytes));
    }

    [Fact]
    public void A_truncated_header_is_not_a_psf_file() =>
        Assert.Equal("sce_sys/param.sfo is not a valid PSF file.", ReadError(TestSfo.Bloodborne[..12]));

    [Fact]
    public void More_entries_than_the_file_holds_is_not_a_psf_file()
    {
        var bytes = TestSfo.Bloodborne.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 1000);

        Assert.Equal("sce_sys/param.sfo is not a valid PSF file.", ReadError(bytes));
    }

    [Fact]
    public void A_data_offset_past_the_end_is_not_a_psf_file()
    {
        var bytes = TestSfo.Bloodborne.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20 + 12), 100_000);

        Assert.Equal("sce_sys/param.sfo is not a valid PSF file.", ReadError(bytes));
    }

    [Fact]
    public void A_key_offset_past_the_end_is_not_a_psf_file()
    {
        var bytes = TestSfo.Bloodborne.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), 60_000);

        Assert.Equal("sce_sys/param.sfo is not a valid PSF file.", ReadError(bytes));
    }

    [Fact]
    public void A_missing_title_id_is_named() =>
        Assert.Equal("sce_sys/param.sfo has no TITLE_ID.", ReadError(TestSfo.Texts(("TITLE", "Bloodborne®"))));

    [Fact]
    public void A_title_id_that_is_not_a_folder_name_is_refused() =>
        Assert.Equal("Title ID \"CUSA-03173\" must contain only letters and digits.", ReadError(TestSfo.Texts(("TITLE_ID", "CUSA-03173"))));

    public void Dispose() => _temp.Dispose();
}
