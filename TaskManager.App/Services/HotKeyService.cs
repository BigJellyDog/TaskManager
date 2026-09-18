namespace TaskManager.App.Services;

/// <summary>Действие, которое страница выполняет по горячей клавише (общие переходы сервис делает сам).</summary>
public enum HotKeyAction
{
    /// <summary>Ctrl+K: перейти в строку поиска.</summary>
    Search,

    /// <summary>Ctrl+Enter: сохранить форму.</summary>
    Save,

    /// <summary>Esc: отменить и вернуться.</summary>
    Cancel,

    /// <summary>Ctrl+D: выполнить задачу на карточке.</summary>
    Complete,
}

/// <summary>
/// Горячие клавиши окна (как в макете Signal): Ctrl+1…6 переходят по разделам, Ctrl+N открывает новую задачу,
/// Alt+← возвращает назад. Остальные (Ctrl+K, Ctrl+Enter, Esc, Ctrl+D) сервис передаёт открытой странице событием
/// <see cref="Pressed"/>: у неё есть нужные команды.
/// </summary>
public sealed class HotKeyService(INavigationService navigation)
{
    private static readonly string[] Sections = ["tasks", "statistics", "data", "trash", "tags", "settings"];

    /// <summary>Нажата клавиша, которую обрабатывает страница.</summary>
    public event Action<HotKeyAction>? Pressed;

    /// <summary>Подключается к окну и слушает клавиши, даже когда фокус в поле ввода.</summary>
    public void Attach(Window window)
    {
#if WINDOWS
        if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native && native.Content is { } root)
        {
            // Перехватываем до поля ввода (PreviewKeyDown): иначе Ctrl+N вписал бы «n» в строку поиска, а Ctrl+Enter и Esc не дошли бы до нас.
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
