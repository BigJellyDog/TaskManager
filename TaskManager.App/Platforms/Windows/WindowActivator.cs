namespace TaskManager.App.Platforms.Windows;

using System.Runtime.InteropServices;

/// <summary>
/// Brings the application window to the front at start-up. Windows does not let a process that is not in focus (for example
/// one started from Explorer, a console or an IDE) simply take the focus, so the window is raised in several ways:
/// restore, a brief "always on top" and a request for the foreground.
/// </summary>
public static class WindowActivator
{
    private const int SwRestore = 9;
    private const int SwShow = 5;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpShowWindow = 0x0040;
    private const byte VkMenu = 0x12;
    private const uint KeyEventKeyUp = 0x0002;

    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNotTopmost = new(-2);

    /// <summary>Raises the window with the handle <paramref name="handle"/> above the others and gives it the focus.</summary>
    public static void BringToFront(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (IsIconic(handle))
        {
            ShowWindow(handle, SwRestore);
        }
        else
        {
            ShowWindow(handle, SwShow);
        }

        // Pressing Alt lifts the Windows restriction on changing the foreground window for this process.
        keybd_event(VkMenu, 0, 0, UIntPtr.Zero);
        keybd_event(VkMenu, 0, KeyEventKeyUp, UIntPtr.Zero);

        // Briefly "always on top" and straight back: the window ends up above the others but is not pinned.
        SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
        SetWindowPos(handle, HwndNotTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
        SetForegroundWindow(handle);
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
