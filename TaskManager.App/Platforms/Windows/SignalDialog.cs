namespace TaskManager.App.Platforms.Windows;

using Microsoft.Maui.Platform;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

/// <summary>
/// A message or confirmation window in the Signal style: the surface and the border from tokens, the title in a monospaced font,
/// its own buttons. It is a ContentDialog without the standard buttons, so the window is always in the theme of the application
/// (the standard buttons and background are drawn by Windows in its own theme).
/// </summary>
public static class SignalDialog
{
    // Only one ContentDialog can be open in a window: the next ones wait for their turn.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static global::Windows.UI.Color Token(string key)
    {
        var color = Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
                    && value is Microsoft.Maui.Graphics.Color c
            ? c
            : Microsoft.Maui.Graphics.Colors.Gray;

        return color.ToWindowsColor();
    }

    private static SolidColorBrush Brush(string key) => new(Token(key));

    /// <summary>Shows the window. <paramref name="cancel"/> equal to null: one button (a message). Returns true when the primary button is pressed.</summary>
    public static async Task<bool> ShowAsync(string title, string message, string accept, string? cancel)
    {
        var root = (Microsoft.Maui.Controls.Application.Current?.Windows is { Count: > 0 } windows
                    ? windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window
                    : null)?.Content?.XamlRoot;
        if (root is null)
        {
            return false;
        }

        await Gate.WaitAsync();
        try
        {
            var accepted = false;
            var dialog = new ContentDialog
            {
                XamlRoot = root,
                RequestedTheme = global::TaskManager.App.Services.ThemeManager.NativeTheme,
                Background = Brush("Surface"),
                BorderBrush = Brush("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(0),
            };

            var none = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            var raised = Brush("SurfaceRaised");

            Border Button(string text, bool primary, bool result)
            {
                var label = new TextBlock
                {
                    Text = text,
                    Foreground = Brush(primary ? "OnAccent" : "Ink"),
                    FontFamily = new FontFamily("Segoe UI Variable"),
                    FontSize = 13,
                    FontWeight = primary ? FontWeights.Bold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                var normal = primary ? Brush("Accent") : Brush("Surface");
                var hover = primary ? Brush("Accent") : raised;
                var button = new Border
                {
                    Child = label,
                    Background = normal,
                    BorderBrush = Brush(primary ? "Accent" : "Line"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(16, 0, 16, 0),
                    MinHeight = 32,
                    MinWidth = 84,
                };

                button.PointerEntered += (_, _) => { button.Background = hover; button.Opacity = primary ? 0.92 : 1; };
                button.PointerExited += (_, _) => { button.Background = normal; button.Opacity = 1; };
                button.Tapped += (_, _) =>
                {
                    accepted = result;
                    dialog.Hide();
                };

                return button;
            }

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 6, 0, 0),
            };
            if (cancel is not null)
            {
                buttons.Children.Add(Button(cancel, primary: false, result: false));
            }

            buttons.Children.Add(Button(accept, primary: true, result: true));

            var content = new StackPanel
            {
                Spacing = 12,
                Padding = new Thickness(22, 18, 22, 18),
                MinWidth = 340,
                MaxWidth = 460,
            };
            content.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brush("Ink"),
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
            });
            content.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = Brush("InkMuted"),
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            });
            content.Children.Add(buttons);

            dialog.Content = content;

            // Enter confirms, Esc closes the window as a cancel (the standard ContentDialog behaviour).
            dialog.KeyDown += (_, e) =>
            {
                if (e.Key == global::Windows.System.VirtualKey.Enter)
                {
                    accepted = true;
                    dialog.Hide();
                    e.Handled = true;
                }
            };

            await dialog.ShowAsync();
            return accepted;
        }
        finally
        {
            Gate.Release();
        }
    }
}
