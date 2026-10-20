using System.Security.Cryptography;

namespace PS5Kitsune.Core.IO;

public static class FileHash
{
    private const int BufferSize = 81920;

    /// <summary>Lowercase hex SHA-256 of a file.</summary>
    public static string Sha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>Lowercase hex SHA-256 of a file.</summary>
    public static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>True when the file exists and has this SHA-256.</summary>
    public static bool Matches(string path, string sha256) => File.Exists(path) && Sha256(path) == sha256;
}
