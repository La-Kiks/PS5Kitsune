using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Views;

namespace PS5Kitsune.App.Shell;

/// <param name="owner">The main window, once it exists.</param>
public sealed class AvaloniaDialogService(Func<Window?> owner) : IDialogService
{
    public async Task<string?> PickFolderAsync(string title, string? startFolder)
    {
        if (owner() is not { } window)
        {
            return null;
        }

        var start = startFolder is null ? null : await window.StorageProvider.TryGetFolderFromPathAsync(startFolder).ConfigureAwait(true);
        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        }).ConfigureAwait(true);
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        if (owner() is not { } window)
        {
            return true;
        }

        return await new ConfirmWindow(title, message, confirmText).ShowDialog<bool>(window).ConfigureAwait(true);
    }
}
