namespace TaskManager.App.Controls;

/// <summary>
/// Строка выпадающего списка. <see cref="Level"/> больше нуля рисует значок приоритета (столбики) в цвете
/// <see cref="ColorKey"/>; <see cref="Muted"/> делает текст приглушённым (теги).
/// </summary>
public sealed record DropEntry(string Text, int Level = 0, string? ColorKey = null, bool Muted = false);

/// <summary>Открывает список в стиле Signal под элементом (чипом, полем). Сейчас только Windows.</summary>
public static class DropMenu
{
    public static void Show(VisualElement anchor, IReadOnlyList<DropEntry> entries, int selected, Action<int> chosen, bool mono)
    {
#if WINDOWS
        if (anchor.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement native && entries.Count > 0)
        {
            Platforms.Windows.SignalMenu.ShowList(native, entries, selected, chosen, mono);
        }
#endif
    }
}
