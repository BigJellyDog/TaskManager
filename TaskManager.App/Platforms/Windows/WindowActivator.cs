namespace TaskManager.App.Platforms.Windows;

using System.Runtime.InteropServices;

/// <summary>
/// Выводит окно приложения на передний план при запуске. Windows не даёт процессу, который не в фокусе (например,
/// запущенному из Проводника, консоли или IDE), просто забрать фокус, поэтому окно поднимается несколькими способами:
/// восстановление, кратковременное «поверх всех» и запрос на передний план.
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

    /// <summary>Поднимает окно с описателем <paramref name="handle"/> поверх остальных и передаёт ему фокус.</summary>
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

        // Нажатие Alt снимает ограничение Windows на смену окна переднего плана для этого процесса.
        keybd_event(VkMenu, 0, 0, UIntPtr.Zero);
        keybd_event(VkMenu, 0, KeyEventKeyUp, UIntPtr.Zero);

        // Кратко «поверх всех» и сразу обратно: окно оказывается выше остальных, но не остаётся закреплённым.
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
