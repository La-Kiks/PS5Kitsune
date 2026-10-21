using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PS5Kitsune.App.Views;

/// <summary>A yes/no question. Closing it with the title bar counts as no.</summary>
public sealed class ConfirmWindow : Window
{
    public ConfirmWindow(string title, string message, string confirmText)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var confirm = new Button { Content = confirmText };
        confirm.Click += (_, _) => Close(true);
        var cancel = new Button { Content = "Cancel", IsDefault = true, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { confirm, cancel },
                },
            },
        };
    }
}
