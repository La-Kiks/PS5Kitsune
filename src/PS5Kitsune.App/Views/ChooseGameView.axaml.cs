using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class ChooseGameView : UserControl
{
    public ChooseGameView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private static string? SinglePath(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.ToList() is [var item] ? item.TryGetLocalPath() : null;

    private static void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = SinglePath(e) is null ? DragDropEffects.None : DragDropEffects.Copy;

    /// <summary>The same path the pickers give: the view model decides whether it is a game.</summary>
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (SinglePath(e) is { } path && DataContext is ChooseGameViewModel viewModel)
        {
            await viewModel.OpenAsync(path).ConfigureAwait(true);
        }
    }
}
