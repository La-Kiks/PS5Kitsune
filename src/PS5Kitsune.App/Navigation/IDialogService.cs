namespace PS5Kitsune.App.Navigation;

public interface IDialogService
{
    /// <returns>The chosen folder's local path; null when the user cancels.</returns>
    Task<string?> PickFolderAsync(string title, string? startFolder);

    /// <returns>True when the user confirms.</returns>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);
}
