using System.Diagnostics;

namespace TaskManager.App.Services;

public sealed class AppRestarter : IAppRestarter
{
    public void Restart()
    {
        // Новый экземпляр запускается до закрытия этого: при старте он сам дождётся, пока файл базы освободится.
        if (Environment.ProcessPath is { } path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        // Quit закроет окно, а закрытие окна останавливает фоновый сервис (AppInitializer.Shutdown).
        Application.Current?.Quit();
    }
}
