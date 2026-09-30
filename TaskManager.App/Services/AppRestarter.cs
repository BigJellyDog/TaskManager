using System.Diagnostics;

namespace TaskManager.App.Services;

public sealed class AppRestarter : IAppRestarter
{
    public void Restart()
    {
        // The new instance is started before this one closes: on start it waits by itself until the database file is released.
        if (Environment.ProcessPath is { } path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        // Quit closes the window, and closing the window stops the background service (AppInitializer.Shutdown).
        Application.Current?.Quit();
    }
}
