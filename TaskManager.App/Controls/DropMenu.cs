namespace TaskManager.App.Controls;

/// <summary>
/// A row of the drop-down list. A <see cref="Level"/> above zero draws the priority icon (bars) in the colour
/// <see cref="ColorKey"/>; <see cref="Muted"/> makes the text muted (tags); <see cref="Hint"/> is a label on the right (language code); <see cref="Swatch"/> is the three colours of a theme sample.
/// </summary>
public sealed record DropEntry(string Text, int Level = 0, string? ColorKey = null, bool Muted = false, string? Hint = null, string[]? Swatch = null);

/// <summary>Opens a list in the Signal style under an element (chip, field). Windows only for now.</summary>
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
