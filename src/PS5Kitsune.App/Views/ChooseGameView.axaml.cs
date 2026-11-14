using Avalonia.Input;
using Avalonia.Platform.Storage;
using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class ChooseGameView : PageView
{
    public ChooseGameView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => Highlight(false));
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void Highlight(bool on) => DropZone.Classes.Set("over", on);

    private static string? SinglePath(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.ToList() is [var item] ? item.TryGetLocalPath() : null;

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var accepted = SinglePath(e) is not null;
        e.DragEffects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        Highlight(accepted);
    }

    /// <summary>The same path the pickers give: the view model decides whether it is a game.</summary>
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        Highlight(false);
        if (SinglePath(e) is { } path && DataContext is ChooseGameViewModel viewModel)
        {
            await viewModel.OpenAsync(path).ConfigureAwait(true);
        }
    }
}
