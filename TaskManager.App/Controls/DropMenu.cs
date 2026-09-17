namespace TaskManager.App.Controls;

/// <summary>Открывает список в стиле Signal под элементом (чипом, полем). Сейчас только Windows.</summary>
public static class DropMenu
{
    public static void Show(VisualElement anchor, IReadOnlyList<string> texts, int selected, Action<int> chosen, bool mono)
    {
#if WINDOWS
        if (anchor.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement native && texts.Count > 0)
        {
            Platforms.Windows.SignalMenu.ShowList(native, texts, selected, chosen, mono);
        }
#endif
    }
}
