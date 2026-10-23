namespace PS5Kitsune.App.Navigation;

public interface IDialogService
{
    /// <returns>The chosen folder's local path; null when the user cancels.</returns>
    Task<string?> PickFolderAsync(string title, string? startFolder);

    /// <param name="extension">The file extension to filter on, without the dot (e.g. "zip").</param>
    /// <returns>The chosen file's local path; null when the user cancels.</returns>
    Task<string?> PickFileAsync(string title, string extension);

    /// <returns>True when the user confirms.</returns>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);
}
