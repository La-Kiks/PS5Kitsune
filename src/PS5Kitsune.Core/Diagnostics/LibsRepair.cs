using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Diagnostics;

/// <summary>
/// Repair libs, the fix for errors 126 and 193: deletes the game's libs and copies them again from the install that converted the game.
/// The libs must match the conversion, so the active install is not used. See the 7c spec, "Fixes".
/// </summary>
public sealed class LibsRepair(GameLibrary library, IAnyPS5Installer installer, AnyPS5Paths paths, RunningGames running)
{
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game is running: its libs are in use.</exception>
    /// <exception cref="LibsSourceChangedException">The install that converted the game is missing or has another version.</exception>
    public async Task RepairAsync(string titleId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(titleId);
        var game = library.Layout(titleId);
        using var held = GameLock.TryAcquire(game) ?? throw new GameBusyException(titleId);
        var record = library.ReadHeld(game);
        if (running.Check(game) is { } current)
        {
            throw new GameRunningException(record.Name, current);
        }

        var convertedWith = record.ConvertedWith;
        if (InstallModes.Parse(convertedWith.Mode) is not { } mode
            || installer.ReadRecord(mode) is not { } install
            || install.Version != convertedWith.Version
            || !Directory.Exists(paths.For(mode).Libs))
        {
            throw new LibsSourceChangedException(convertedWith.Mode, record.Name);
        }

        var source = paths.For(mode).Libs;
        await Task.Run(
            () =>
            {
                if (Directory.Exists(game.Libs))
                {
                    Directory.Delete(game.Libs, recursive: true);
                }

                FileTree.Copy(source, game.Libs, relative => relative == ZipExtraction.MarkerFileName, cancellationToken);
            },
            cancellationToken).ConfigureAwait(false);
    }
}
