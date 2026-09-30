namespace TaskManager.App.Services;

/// <summary>An action the page performs on a shortcut key (the general navigation is done by the service itself).</summary>
public enum HotKeyAction
{
    /// <summary>Ctrl+K: go to the search field.</summary>
    Search,

    /// <summary>Ctrl+Enter: save the form.</summary>
    Save,

    /// <summary>Esc: cancel and go back.</summary>
    Cancel,

    /// <summary>Ctrl+D: complete the task on the card.</summary>
    Complete,
}

/// <summary>
/// Window shortcuts (as in the Signal mockup): Ctrl+1…6 go to the sections, Ctrl+N opens a new task,
/// Alt+← goes back. The others (Ctrl+K, Ctrl+Enter, Esc, Ctrl+D) the service passes to the open page with the event
/// <see cref="Pressed"/>: it has the commands needed.
/// </summary>
public sealed class HotKeyService(INavigationService navigation)
{
    private static readonly string[] Sections = ["tasks", "statistics", "data", "trash", "tags", "settings"];

    /// <summary>A key handled by the page was pressed.</summary>
    public event Action<HotKeyAction>? Pressed;

    /// <summary>Attaches to the window and listens to keys even when the focus is in an input field.</summary>
    public void Attach(Window window)
    {
#if WINDOWS
        if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native && native.Content is { } root)
        {
            // We intercept before the input field (PreviewKeyDown): otherwise Ctrl+N would type "n" into the search box, and Ctrl+Enter and Esc would not reach us.
            root.AddHandler(
                Microsoft.UI.Xaml.UIElement.PreviewKeyDownEvent,
                new Microsoft.UI.Xaml.Input.KeyEventHandler((_, e) => OnKeyDown(e)),
                handledEventsToo: true);
        }
#endif
    }

#if WINDOWS
    private static bool IsDown(Windows.System.VirtualKey key) =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private void OnKeyDown(Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var ctrl = IsDown(Windows.System.VirtualKey.Control);
        var alt = IsDown(Windows.System.VirtualKey.Menu);
        var key = e.Key;

        if (ctrl && !alt)
        {
            var number = key - Windows.System.VirtualKey.Number1;
            if (number is >= 0 and < 6)
            {
                Navigate("//" + Sections[number]);
                e.Handled = true;
                return;
            }

            switch (key)
            {
                case Windows.System.VirtualKey.N:
                    Navigate("taskedit?id=0");
                    e.Handled = true;
                    return;
                case Windows.System.VirtualKey.K:
                    Raise(HotKeyAction.Search);
                    e.Handled = true;
                    return;
                case Windows.System.VirtualKey.Enter:
                    Raise(HotKeyAction.Save);
                    e.Handled = true;
                    return;
                case Windows.System.VirtualKey.D:
                    Raise(HotKeyAction.Complete);
                    e.Handled = true;
                    return;
            }
        }
        else if (alt && !ctrl && key == Windows.System.VirtualKey.Left)
        {
            _ = navigation.GoBackAsync();
            e.Handled = true;
        }
        else if (!ctrl && !alt && key == Windows.System.VirtualKey.Escape)
        {
            Raise(HotKeyAction.Cancel);
        }
    }
#endif

    private void Raise(HotKeyAction action) => Pressed?.Invoke(action);

    private void Navigate(string route) => _ = navigation.GoToAsync(route);
}
