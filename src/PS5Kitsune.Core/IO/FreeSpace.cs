namespace PS5Kitsune.Core.IO;

public interface IFreeSpace
{
    /// <summary>Free bytes on the drive holding <paramref name="path"/>; null when it cannot be read.</summary>
    long? FreeBytes(string path);
}

public sealed class DriveFreeSpace : IFreeSpace
{
    public long? FreeBytes(string path)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path)) ?? path).AvailableFreeSpace;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
