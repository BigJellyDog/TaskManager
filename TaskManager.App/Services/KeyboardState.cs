namespace TaskManager.App.Services;

/// <summary>State of modifier keys at the moment of a click (the MAUI tap event does not carry it).</summary>
internal static class KeyboardState
{
    /// <summary>Whether Shift is held down right now.</summary>
    public static bool IsShiftDown
    {
        get
        {
#if WINDOWS
            return Microsoft.UI.Input.InputKeyboardSource
                .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
#else
            return false;
#endif
        }
    }
}
