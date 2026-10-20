using System.Text;

namespace PS5Kitsune.Core.Library;

/// <summary>Follows a log file another process writes to, line by line.</summary>
public static class LogTail
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Calls <paramref name="onLine"/> for each line written to <paramref name="path"/> until <paramref name="stop"/>
    /// completes, then for the rest of the file (a last line without a newline included).
    /// </summary>
    public static async Task FollowAsync(string path, Action<string> onLine, Task stop, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onLine);
        ArgumentNullException.ThrowIfNull(stop);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var pending = new StringBuilder();
        var buffer = new char[4096];
        while (true)
        {
            var finished = stop.IsCompleted;
            int read;
            while ((read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                Emit(read);
            }

            if (finished)
            {
                break;
            }

            await Task.WhenAny(stop, Task.Delay(PollInterval, cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (pending.Length > 0)
        {
            onLine(pending.ToString());
        }

        void Emit(int count)
        {
            for (var index = 0; index < count; index++)
            {
                if (buffer[index] == '\n')
                {
                    onLine(pending.ToString().TrimEnd('\r'));
                    pending.Clear();
                }
                else
                {
                    pending.Append(buffer[index]);
                }
            }
        }
    }
}
